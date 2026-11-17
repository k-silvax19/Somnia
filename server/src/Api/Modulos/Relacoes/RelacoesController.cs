using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Somnia.Api.Compartilhado.Auth;
using Somnia.Api.Compartilhado.Http;
using Somnia.Aplicacao.Modulos.Relacoes;
using Somnia.Dominio.Modulos.Relacoes;

namespace Somnia.Api.Modulos.Relacoes;

[ApiController]
[Authorize]
[ValidarOrigemAuth]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[Route("api/users/{username}")]
public sealed class RelacoesController(IMediator mediator) : ControllerBase
{
    [HttpPut("follow")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Seguir(string username, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue(JwtRegisteredClaimNames.Sub), out var usuarioId))
            return Unauthorized();
        var resultado = await mediator.Send(new SeguirPerfilCommand(usuarioId, username), ct);
        return resultado.IsFailed ? this.ResultProblem(resultado) : NoContent();
    }

    [HttpDelete("follow")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeixarDeSeguir(string username, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue(JwtRegisteredClaimNames.Sub), out var usuarioId))
            return Unauthorized();
        var resultado = await mediator.Send(new DeixarDeSeguirPerfilCommand(usuarioId, username), ct);
        return resultado.IsFailed ? this.ResultProblem(resultado) : NoContent();
    }

    [HttpGet("connections")]
    [ProducesResponseType<ResumoRelacoesDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Resumo(string username, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue(JwtRegisteredClaimNames.Sub), out var usuarioId))
            return Unauthorized();
        var resultado = await mediator.Send(new ObterResumoRelacoesQuery(usuarioId, username), ct);
        return resultado.IsFailed ? this.ResultProblem(resultado) : Ok(resultado.Value);
    }

    [HttpGet("followers")]
    [ProducesResponseType<PaginaRelacoesDto>(StatusCodes.Status200OK)]
    public Task<IActionResult> Seguidores(string username, CancellationToken ct,
        [FromQuery] int limit = 20, [FromQuery] int offset = 0) =>
        ListarAsync(username, TipoListaRelacoes.Seguidores, limit, offset, ct);

    [HttpGet("following")]
    [ProducesResponseType<PaginaRelacoesDto>(StatusCodes.Status200OK)]
    public Task<IActionResult> Seguidos(string username, CancellationToken ct,
        [FromQuery] int limit = 20, [FromQuery] int offset = 0) =>
        ListarAsync(username, TipoListaRelacoes.Seguidos, limit, offset, ct);

    private async Task<IActionResult> ListarAsync(string username, TipoListaRelacoes tipo, int limit, int offset, CancellationToken ct)
    {
        var resultado = await mediator.Send(new ListarRelacoesQuery(username, tipo, limit, offset), ct);
        return resultado.IsFailed ? this.ResultProblem(resultado) : Ok(resultado.Value);
    }
}
