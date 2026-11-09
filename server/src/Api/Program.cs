using System.IdentityModel.Tokens.Jwt;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Somnia.Api.Compartilhado.Auth;
using Somnia.Api.Compartilhado.Http;
using Somnia.Aplicacao;
using Somnia.Dominio.Compartilhado;
using Somnia.Infraestrutura;
using Somnia.Infraestrutura.Compartilhado.Orm;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructureServices(builder.Configuration);
builder.Services.AddApplicationServices(builder.Configuration);
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
    options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true);

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
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "Somnia API");
        options.UseRequestInterceptor("request => { request.headers['X-Somnia-Client'] = 'web'; const csrf = document.cookie.split('; ').find(cookie => cookie.startsWith('__Host-somnia-csrf=')); if (csrf) request.headers['X-CSRF-Token'] = decodeURIComponent(csrf.substring(csrf.indexOf('=') + 1)); return request; }");
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

app.UseExceptionHandler(error => error.Run(async context =>
{
    context.Response.StatusCode = 500;
    context.Response.ContentType = "application/problem+json";
    await context.Response.WriteAsJsonAsync(
        new Microsoft.AspNetCore.Mvc.ProblemDetails
        {
            Status = 500,
            Title = "Erro interno do servidor"
        });
}));

app.UseStatusCodePages();
app.UseHttpsRedirection();
app.UseRouting();
app.UseCors("Client");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();