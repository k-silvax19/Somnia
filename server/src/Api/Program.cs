using System.IdentityModel.Tokens.Jwt;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Somnia.Api.Compartilhado.Auth;
using Somnia.Api.Compartilhado.Http;
using Somnia.Aplicacao;
using Somnia.Dominio.Compartilhado;
using Somnia.Infraestrutura;
using Somnia.Infraestrutura.Compartilhado.Mensageria;
using Somnia.Infraestrutura.Compartilhado.Orm;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructureServices(builder.Configuration);
builder.Services.AddApplicationServices(builder.Configuration);
builder.Services.AddAvatarMessaging(builder.Configuration);
builder.Services.AddScoped<IEmissorTokens, JwtTokenGenerator>();
builder.Services
    .AddOptions<JwtOptions>()
    .BindConfiguration(JwtOptions.SectionName)
    .Validate(x => !string.IsNullOrWhiteSpace(x.Issuer), "Jwt:Issuer é obrigatório.")
    .Validate(x => !string.IsNullOrWhiteSpace(x.Audience), "Jwt:Audience é obrigatório.")
    .Validate(x => Encoding.UTF8.GetByteCount(x.SigningKey) >= 32, "Jwt:SigningKey deve ter ao menos 32 bytes.")
    .Validate(x => x.ExpiresInMinutes is > 0 and <= 15, "Jwt:ExpiresInMinutes inválido.")
    .ValidateOnStart();

var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new();
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = jwt.Issuer,
        ValidateAudience = true,
        ValidAudience = jwt.Audience,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
        NameClaimType = JwtRegisteredClaimNames.Sub,
        ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
        ClockSkew = TimeSpan.FromSeconds(30)
    };

    options.MapInboundClaims = false;
    options.IncludeErrorDetails = false;
    options.Events = new JwtBearerEvents
    {
        OnTokenValidated = async context =>
        {
            var jti = context.Principal?.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
            if (string.IsNullOrWhiteSpace(jti))
            {
                context.Fail("Token sem identificador.");
                return;
            }

            var sessoes = context.HttpContext.RequestServices.GetRequiredService<IRefreshTokens>();
            if (!Guid.TryParse(context.Principal?.FindFirst("sid")?.Value, out var sessaoId)
                || !Guid.TryParse(context.Principal?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var usuarioId)
                || !await sessoes.SessaoEstaAtivaAsync(sessaoId, usuarioId, context.HttpContext.RequestAborted))
            {
                context.Fail("Sessao encerrada ou expirada.");
                return;
            }

            var revocation = context.HttpContext.RequestServices.GetRequiredService<IRevogacaoToken>();

            if (await revocation.EstaRevogadoAsync(jti, context.HttpContext.RequestAborted))
            {
                context.Fail("Token revogado.");
            }
        }
    };
});

builder.Services
    .AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build());

builder.Services.AddControllers(options =>
{
    options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
    options.Filters.Add<ApiProblemDetailsFilter>();
    options.ModelMetadataDetailsProviders.Add(new Microsoft.AspNetCore.Mvc.ModelBinding.Metadata.SystemTextJsonValidationMetadataProvider());
});
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
    ApiProblemDetails.Completar(context.HttpContext, context.ProblemDetails));

builder.Services.AddOpenApiServices();

var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddPolicy("Client", policy =>
{
    if (origins.Length > 0)
    {
        policy.WithOrigins(origins);
    }

    policy.AllowAnyHeader().AllowAnyMethod().AllowCredentials();
}));

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, ct) =>
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var espera))
            context.HttpContext.Response.Headers.RetryAfter = Math.Max(1, (int)Math.Ceiling(espera.TotalSeconds))
                .ToString(System.Globalization.CultureInfo.InvariantCulture);
        await ApiProblemDetails.EscreverAsync(context.HttpContext, StatusCodes.Status429TooManyRequests, ct);
    };
    foreach (var (nome, limite) in new[] { ("login", 10), ("register", 5), ("refresh", 30) })
    {
        options.AddPolicy(nome, context => RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = limite,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
    }
    options.AddPolicy("avatar", context => RateLimitPartition.GetFixedWindowLimiter(
        context.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        }));
});

var app = builder.Build();

app.UseExceptionHandler(error => error.Run(context =>
{
    var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;
    var status = exception is BadHttpRequestException requestException
        ? requestException.StatusCode : StatusCodes.Status500InternalServerError;
    return ApiProblemDetails.EscreverAsync(context, status, context.RequestAborted);
}));
app.UseStatusCodePages(context => ApiProblemDetails.EscreverAsync(context.HttpContext,
    context.HttpContext.Response.StatusCode, context.HttpContext.RequestAborted));

app.UseHttpsRedirection();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "Somnia API");
        options.UseRequestInterceptor("function (request) { request.headers['X-Somnia-Client'] = 'web'; const csrf = document.cookie.split('; ').find(cookie => cookie.startsWith('__Host-somnia-csrf=')); if (csrf) request.headers['X-CSRF-Token'] = decodeURIComponent(csrf.substring(csrf.indexOf('=') + 1)); return request; }");
    });
}
else
{
    app.UseHsts();
}

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<SomniaDbContext>();

    if (db.Database.IsSqlServer())
    {
        db.Database.Migrate();
    }
}

app.UseRouting();
app.UseCors("Client");
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();
app.MapControllers();
app.Run();
