using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Somnia.Api.Compartilhado.Auth;
using MediatR;
using Somnia.Api.Compartilhado.Http;
using Somnia.Aplicacao.Modulos.Auth;

namespace Somnia.Api.Modulos.Auth;

public sealed record RegistroRequest(
    [Required] string? Email,
    [Required, StringLength(256), RegularExpression(@"[a-zA-Z0-9._-]+")] string? Username,
    [Required] string? Password,
    [Required] string? DisplayName
);
public sealed record AutenticacaoRequest(string? Email, string? Password);

[ApiController]
[ValidarOrigemAuth]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[Route("api/auth")]
public sealed class AuthController(IMediator mediator, IOptions<JwtOptions> jwtOptions) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("register")]
    [EnableRateLimiting("register")]
    public async Task<IActionResult> Register(RegistroRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new CadastrarUsuarioCommand(
            request.DisplayName, request.Email, request.Password, request.Username), ct);

        if (result.IsFailed)
        {
            return this.ResultProblem(result);
        }

        return StatusCode(StatusCodes.Status201Created, result.Value);
    }

    [AllowAnonymous]
    [HttpPost("login")]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> Login(AutenticacaoRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new AutenticarUsuarioCommand(request.Email, request.Password), ct);

        if (result.IsFailed)
        {
            return this.ResultProblem(result);
        }

        return Ok(CookiesAutenticacao.Definir(Response, result.Value, jwtOptions.Value.SigningKey));
    }

    [AllowAnonymous]
    [HttpPost("refresh")]
    [EnableRateLimiting("refresh")]
    public async Task<IActionResult> Refresh(CancellationToken ct)
    {
        if (!CookiesAutenticacao.ValidarCsrf(Request, jwtOptions.Value.SigningKey))
            return CookiesAutenticacao.ErroCsrf();

        var result = await mediator.Send(new RenovarSessaoCommand(
            Request.Cookies[CookiesAutenticacao.RefreshCookie]), ct);

        if (result.IsFailed)
        {
            CookiesAutenticacao.Remover(Response);
            return this.ResultProblem(result);
        }

        return Ok(CookiesAutenticacao.Definir(Response, result.Value, jwtOptions.Value.SigningKey));
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        if (!CookiesAutenticacao.ValidarCsrf(Request, jwtOptions.Value.SigningKey))
            return CookiesAutenticacao.ErroCsrf();

        var jti = User.FindFirstValue(JwtRegisteredClaimNames.Jti);
        var exp = User.FindFirstValue(JwtRegisteredClaimNames.Exp);
        if (!Guid.TryParse(User.FindFirstValue(JwtRegisteredClaimNames.Sub), out var usuarioId)
            || string.IsNullOrWhiteSpace(jti) || !long.TryParse(exp, out var unixExp)
            || unixExp < 0 || unixExp > 253402300799)
        {
            return Unauthorized();
        }

        var expirationUtc = DateTimeOffset.FromUnixTimeSeconds(unixExp).UtcDateTime;

        var result = await mediator.Send(new EncerrarSessaoCommand(jti, expirationUtc, usuarioId), ct);

        if (result.IsFailed)
        {
            return this.ResultProblem(result);
        }

        CookiesAutenticacao.Remover(Response);
        return NoContent();
    }
}
