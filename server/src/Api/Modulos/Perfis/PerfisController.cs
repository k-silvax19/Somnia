using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Somnia.Api.Compartilhado.Auth;
using Somnia.Api.Compartilhado.Http;
using Somnia.Aplicacao.Modulos.Auth;
using Somnia.Aplicacao.Modulos.Perfis;

namespace Somnia.Api.Modulos.Perfis;

[ApiController]
[ValidarOrigemAuth]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[Route("api")]
public sealed class PerfisController(IMediator mediator) : ControllerBase
{
    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue(JwtRegisteredClaimNames.Sub), out var usuarioId))
            return Unauthorized();

        var result = await mediator.Send(new ObterUsuarioQuery(usuarioId), ct);
        if (result.IsFailed)
            return this.ResultProblem(result);

        return Ok(result.Value);
    }

    [HttpPatch("me/profile")]
    public async Task<IActionResult> Atualizar(AtualizarPerfilRequest request, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue(JwtRegisteredClaimNames.Sub), out var usuarioId))
            return Unauthorized();

        var result = await mediator.Send(new AtualizarPerfilCommand(usuarioId, request.ToAlteracao()), ct);
        if (result.IsFailed)
            return this.ResultProblem(result);

        return Ok(result.Value);
    }

    [HttpGet("users/{username}")]
    public async Task<IActionResult> Publico(string username, CancellationToken ct)
    {
        var result = await mediator.Send(new ObterPerfilPublicoQuery(username), ct);
        if (result.IsFailed)
            return this.ResultProblem(result);

        return Ok(result.Value);
    }
}
