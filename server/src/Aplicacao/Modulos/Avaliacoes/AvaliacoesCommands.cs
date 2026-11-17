using FluentResults;
using MediatR;
using Somnia.Aplicacao.Compartilhado;
using Somnia.Aplicacao.Modulos.Catalogo;
using Somnia.Dominio.Compartilhado;
using Somnia.Dominio.Modulos.Avaliacoes;
using Somnia.Dominio.Modulos.Catalogo;

namespace Somnia.Aplicacao.Modulos.Avaliacoes;

public sealed record CriarAvaliacaoCommand(Guid UsuarioId, ReferenciaItem Item, int? Score, string? Review)
    : IRequest<Result<AvaliacaoDto>>;
public sealed record AtualizarAvaliacaoCommand(Guid UsuarioId, Guid Id, int? Score, string? Review)
    : IRequest<Result<AvaliacaoDto>>;
public sealed record ExcluirAvaliacaoCommand(Guid UsuarioId, Guid Id) : IRequest<Result>;

public sealed class CriarAvaliacaoCommandHandler(IRepositorioAvaliacao repositorio, IMusicCatalogProvider catalogo)
    : IRequestHandler<CriarAvaliacaoCommand, Result<AvaliacaoDto>>
{
    public async Task<Result<AvaliacaoDto>> Handle(CriarAvaliacaoCommand command, CancellationToken ct)
    {
        var erros = RegrasAvaliacao.ValidarItem(command.Item)
            .Concat(RegrasAvaliacao.ValidarConteudo(command.Score, command.Review)).ToArray();
        if (erros.Length > 0)
            return Result.Fail<AvaliacaoDto>(Erros.Validacao(erros));
        if (await repositorio.ExisteAsync(command.UsuarioId, command.Item, ct))
            return ResultadosAvaliacao.Duplicada();

        // Somente a criação consulta o catálogo; leituras e alterações usam os dados próprios.
        var consulta = await ResultadosCatalogo.ConsultarAsync<ItemCatalogoDto>(async () =>
            command.Item.Type == "album"
                ? await catalogo.ObterAlbumAsync(command.Item.ExternalId, ct)
                : await catalogo.ObterFaixaAsync(command.Item.ExternalId, ct));
        if (consulta.IsFailed)
            return Result.Fail<AvaliacaoDto>(consulta.Errors);
        var item = consulta.Value;
        if (item.Provider != command.Item.Provider || item.Type != command.Item.Type || item.ExternalId != command.Item.ExternalId)
            return Result.Fail<AvaliacaoDto>(Erros.Criar(TipoErro.Validacao,
                "Referência não corresponde ao item do catálogo ativo.", "externalId"));

        var avaliacao = Avaliacao.Criar(command.UsuarioId, command.Item, command.Score, command.Review);
        if (!await repositorio.CriarAsync(avaliacao, ct))
            return ResultadosAvaliacao.Duplicada();
        var salva = await repositorio.ObterAsync(avaliacao.Id, ct);
        return salva is null ? ResultadosAvaliacao.NaoEncontrada<AvaliacaoDto>() : Result.Ok(salva);
    }
}

public sealed class AtualizarAvaliacaoCommandHandler(IRepositorioAvaliacao repositorio)
    : IRequestHandler<AtualizarAvaliacaoCommand, Result<AvaliacaoDto>>
{
    public async Task<Result<AvaliacaoDto>> Handle(AtualizarAvaliacaoCommand command, CancellationToken ct)
    {
        var erros = RegrasAvaliacao.ValidarConteudo(command.Score, command.Review).ToArray();
        if (erros.Length > 0)
            return Result.Fail<AvaliacaoDto>(Erros.Validacao(erros));
        var avaliacao = await repositorio.AtualizarAsync(command.Id, command.UsuarioId,
            command.Score, RegrasAvaliacao.NormalizarResenha(command.Review), ct);
        return avaliacao is null ? ResultadosAvaliacao.NaoEncontrada<AvaliacaoDto>() : Result.Ok(avaliacao);
    }
}

public sealed class ExcluirAvaliacaoCommandHandler(IRepositorioAvaliacao repositorio)
    : IRequestHandler<ExcluirAvaliacaoCommand, Result>
{
    public async Task<Result> Handle(ExcluirAvaliacaoCommand command, CancellationToken ct) =>
        await repositorio.ExcluirAsync(command.Id, command.UsuarioId, ct)
            ? Result.Ok() : Result.Fail(Erros.Criar(TipoErro.NaoEncontrado, "Avaliação inexistente ou não pertence ao titular."));
}

internal static class ResultadosAvaliacao
{
    public static Result<AvaliacaoDto> Duplicada() => Result.Fail<AvaliacaoDto>(Erros.Criar(TipoErro.Conflito,
        "Já existe uma avaliação sua para este item. Use a edição."));
    public static Result<T> NaoEncontrada<T>() => Result.Fail<T>(Erros.Criar(TipoErro.NaoEncontrado,
        "Avaliação inexistente ou não pertence ao titular."));
}
