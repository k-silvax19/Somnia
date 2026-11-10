using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Somnia.Api.Compartilhado.Auth;
using Somnia.Api.Compartilhado.Http;
using Somnia.Aplicacao.Modulos.Auth;
using Somnia.Aplicacao.Modulos.Perfis;
using Somnia.Dominio.Modulos.Perfis;

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

    [HttpPut("me/avatar")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(LimitesAvatar.TamanhoMaximo + 65536)]
    [RequestFormLimits(MultipartBodyLengthLimit = LimitesAvatar.TamanhoMaximo + 65536)]
    public async Task<IActionResult> AtualizarAvatar([FromForm] UploadAvatarRequest request, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue(JwtRegisteredClaimNames.Sub), out var usuarioId))
            return Unauthorized();
        if (request.File is null || request.File.Length is 0 or > LimitesAvatar.TamanhoMaximo)
        {
            ModelState.AddModelError("file", "Envie uma imagem PNG ou JPEG de até 2 MiB.");
            return ValidationProblem(ModelState);
        }

        await using var arquivo = request.File.OpenReadStream();
        using var buffer = new MemoryStream();
        await arquivo.CopyToAsync(buffer, ct);
        var result = await mediator.Send(new AtualizarAvatarCommand(usuarioId, buffer.ToArray()), ct);
        return result.IsFailed ? this.ResultProblem(result) : Ok(result.Value);
    }

    [HttpDelete("me/avatar")]
    public async Task<IActionResult> RemoverAvatar(CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue(JwtRegisteredClaimNames.Sub), out var usuarioId))
            return Unauthorized();
        var result = await mediator.Send(new AtualizarAvatarCommand(usuarioId, null), ct);
        return result.IsFailed ? this.ResultProblem(result) : NoContent();
    }

    [HttpGet("users/{username}/avatar")]
    public async Task<IActionResult> Avatar(string username, CancellationToken ct)
    {
        var result = await mediator.Send(new ObterAvatarQuery(username), ct);
        if (result.IsFailed)
            return this.ResultProblem(result);
        Response.Headers.XContentTypeOptions = "nosniff";
        return File(result.Value, "image/png");
    }
}

public sealed class UploadAvatarRequest
{
    public IFormFile? File { get; set; }
}
