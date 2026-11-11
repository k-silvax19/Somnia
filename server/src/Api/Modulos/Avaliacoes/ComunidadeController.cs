using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Somnia.Api.Compartilhado.Http;
using Somnia.Aplicacao.Modulos.Avaliacoes;
using Somnia.Dominio.Modulos.Avaliacoes;

namespace Somnia.Api.Modulos.Avaliacoes;

[ApiController]
[Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[Route("api")]
public sealed class ComunidadeController(IMediator mediator) : ControllerBase
{
    [HttpGet("feed")]
    [ProducesResponseType<PaginaAvaliacoesDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Feed(CancellationToken ct, [FromQuery] int limit = 20, [FromQuery] int offset = 0)
    {
        if (!Guid.TryParse(User.FindFirstValue(JwtRegisteredClaimNames.Sub), out var usuarioId))
            return Unauthorized();
        var resultado = await mediator.Send(new ListarFeedQuery(usuarioId, limit, offset), ct);
        return resultado.IsFailed ? this.ResultProblem(resultado) : Ok(resultado.Value);
    }

    [HttpGet("discover")]
    [ProducesResponseType<PaginaAvaliacoesDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Descoberta(CancellationToken ct, [FromQuery] int limit = 20, [FromQuery] int offset = 0)
    {
        var resultado = await mediator.Send(new ListarDescobertaQuery(limit, offset), ct);
        return resultado.IsFailed ? this.ResultProblem(resultado) : Ok(resultado.Value);
    }
}
