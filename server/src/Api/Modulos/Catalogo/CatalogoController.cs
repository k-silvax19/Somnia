using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Somnia.Api.Compartilhado.Http;
using Somnia.Aplicacao.Modulos.Catalogo;
using Somnia.Dominio.Modulos.Catalogo;

namespace Somnia.Api.Modulos.Catalogo;

[ApiController]
[Authorize]
[EnableRateLimiting("catalog")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[Route("api/catalog")]
public sealed class CatalogoController(IMediator mediator) : ControllerBase
{
    [HttpGet("search")]
    [ProducesResponseType<PaginaCatalogoDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Pesquisar([FromQuery] string? q, [FromQuery] string? type,
        CancellationToken ct, [FromQuery] int limit = 10, [FromQuery] int offset = 0)
    {
        var resultado = await mediator.Send(new PesquisarCatalogoQuery(q, type, limit, offset), ct);
        return resultado.IsFailed ? this.ResultProblem(resultado) : Ok(resultado.Value);
    }

    [HttpGet("albums/{externalId}")]
    [ProducesResponseType<AlbumCatalogoDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Album(string externalId, CancellationToken ct)
    {
        var resultado = await mediator.Send(new ObterAlbumCatalogoQuery(externalId), ct);
        return resultado.IsFailed ? this.ResultProblem(resultado) : Ok(resultado.Value);
    }

    [HttpGet("tracks/{externalId}")]
    [ProducesResponseType<FaixaCatalogoDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Faixa(string externalId, CancellationToken ct)
    {
        var resultado = await mediator.Send(new ObterFaixaCatalogoQuery(externalId), ct);
        return resultado.IsFailed ? this.ResultProblem(resultado) : Ok(resultado.Value);
    }
}
