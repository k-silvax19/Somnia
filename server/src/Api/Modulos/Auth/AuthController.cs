using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MediatR;
using Somnia.Api.Compartilhado.Http;
using Somnia.Aplicacao.Modulos.Auth;

namespace Somnia.Api.Modulos.Auth;

public sealed record CadastroRequest(string? Nome, string? Email, string? Senha);
public sealed record LoginRequest(string? Email, string? Senha);

[ApiController]
[Route("auth")]
public sealed class AuthController(IMediator mediator) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("cadastro")]
    public async Task<IActionResult> Cadastro(CadastroRequest request, CancellationToken ct)
    {
        var command = new CadastrarUsuarioCommand(request.Nome, request.Email, request.Senha);
        var result = await mediator.Send(command, ct);

        if (result.IsFailed)
        {
            return this.ResultProblem(result);
        }

        return StatusCode(StatusCodes.Status201Created, result.Value);
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken ct)
    {
        var command = new AutenticarUsuarioCommand(request.Email, request.Senha);
        var result = await mediator.Send(command, ct);

        if (result.IsFailed)
        {
            return this.ResultProblem(result);
        }

        return Ok(result.Value);
    }

    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var usuarioId = Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
        var result = await mediator.Send(new ObterUsuarioQuery(usuarioId), ct);

        if (result.IsFailed)
        {
            return this.ResultProblem(result);
        }

        return Ok(result.Value);
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        var jti = User.FindFirstValue(JwtRegisteredClaimNames.Jti);
        var exp = User.FindFirstValue(JwtRegisteredClaimNames.Exp);
        if (string.IsNullOrWhiteSpace(jti) || !long.TryParse(exp, out var unixExp))
        {
            return Unauthorized();
        }

        var expirationUtc = DateTimeOffset.FromUnixTimeSeconds(unixExp).UtcDateTime;

        var result = await mediator.Send(new EncerrarSessaoCommand(jti, expirationUtc), ct);

        if (result.IsFailed)
        {
            return this.ResultProblem(result);
        }

        return NoContent();
    }
}
