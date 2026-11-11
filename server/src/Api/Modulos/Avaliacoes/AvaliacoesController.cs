using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json.Serialization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Somnia.Api.Compartilhado.Auth;
using Somnia.Api.Compartilhado.Http;
using Somnia.Aplicacao.Modulos.Avaliacoes;
using Somnia.Dominio.Modulos.Avaliacoes;

namespace Somnia.Api.Modulos.Avaliacoes;

[ApiController]
[Authorize]
[ValidarOrigemAuth]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[Route("api")]
public sealed class AvaliacoesController(IMediator mediator) : ControllerBase
{
    [HttpPost("reviews")]
    [EnableRateLimiting("catalog")]
    [RequestSizeLimit(65536)]
    [ProducesResponseType<AvaliacaoDto>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Criar(CriarAvaliacaoRequest request, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue(JwtRegisteredClaimNames.Sub), out var usuarioId))
            return Unauthorized();
        var resultado = await mediator.Send(new CriarAvaliacaoCommand(usuarioId,
            new ReferenciaItem(request.Provider!, request.Type!, request.ExternalId!), request.Score, request.Review), ct);
        return resultado.IsFailed ? this.ResultProblem(resultado)
            : CreatedAtAction(nameof(Obter), new { id = resultado.Value.Id }, resultado.Value);
    }

    [HttpPut("reviews/{id:guid}")]
    [RequestSizeLimit(65536)]
    [ProducesResponseType<AvaliacaoDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Atualizar(Guid id, AtualizarAvaliacaoRequest request, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue(JwtRegisteredClaimNames.Sub), out var usuarioId))
            return Unauthorized();
        var resultado = await mediator.Send(new AtualizarAvaliacaoCommand(usuarioId, id, request.Score, request.Review), ct);
        return resultado.IsFailed ? this.ResultProblem(resultado) : Ok(resultado.Value);
    }

    [HttpDelete("reviews/{id:guid}")]
    public async Task<IActionResult> Excluir(Guid id, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue(JwtRegisteredClaimNames.Sub), out var usuarioId))
            return Unauthorized();
        var resultado = await mediator.Send(new ExcluirAvaliacaoCommand(usuarioId, id), ct);
        return resultado.IsFailed ? this.ResultProblem(resultado) : NoContent();
    }

    [HttpGet("reviews/{id:guid}")]
    [ProducesResponseType<AvaliacaoDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Obter(Guid id, CancellationToken ct)
    {
        var resultado = await mediator.Send(new ObterAvaliacaoQuery(id), ct);
        return resultado.IsFailed ? this.ResultProblem(resultado) : Ok(resultado.Value);
    }

    [HttpGet("users/{username}/reviews")]
    [ProducesResponseType<PaginaAvaliacoesDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Perfil(string username, CancellationToken ct,
        [FromQuery] int limit = 20, [FromQuery] int offset = 0)
    {
        var resultado = await mediator.Send(new ListarAvaliacoesPerfilQuery(username, limit, offset), ct);
        return resultado.IsFailed ? this.ResultProblem(resultado) : Ok(resultado.Value);
    }

    [HttpGet("catalog/{provider}/{type}/{externalId}/reviews")]
    [ProducesResponseType<AvaliacoesItemDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Item(string provider, string type, string externalId, CancellationToken ct,
        [FromQuery] int limit = 20, [FromQuery] int offset = 0)
    {
        if (!Guid.TryParse(User.FindFirstValue(JwtRegisteredClaimNames.Sub), out var usuarioId))
            return Unauthorized();
        var resultado = await mediator.Send(new ListarAvaliacoesItemQuery(
            new ReferenciaItem(provider, type, externalId), usuarioId, limit, offset), ct);
        return resultado.IsFailed ? this.ResultProblem(resultado) : Ok(resultado.Value);
    }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class CriarAvaliacaoRequest
{
    [Required] public string? Provider { get; set; }
    [Required] public string? Type { get; set; }
    [Required] public string? ExternalId { get; set; }
    [Range(0, 100)]
    [JsonNumberHandling(JsonNumberHandling.Strict)] public int? Score { get; set; }
    [MaxLength(RegrasAvaliacao.ResenhaMaxima)] public string? Review { get; set; }
}

// PUT substitui todo o conteúdo: campo omitido/nulo remove a nota ou a resenha.
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class AtualizarAvaliacaoRequest
{
    [Range(0, 100)]
    [JsonNumberHandling(JsonNumberHandling.Strict)] public int? Score { get; set; }
    [MaxLength(RegrasAvaliacao.ResenhaMaxima)] public string? Review { get; set; }
}
