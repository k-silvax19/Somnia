using FluentResults;
using MediatR;
using Somnia.Aplicacao.Compartilhado;
using Somnia.Dominio.Compartilhado;
using Somnia.Dominio.Modulos.Catalogo;

namespace Somnia.Aplicacao.Modulos.Catalogo;

public sealed record ObterAlbumCatalogoQuery(string ExternalId) : IRequest<Result<AlbumCatalogoDto>>;
public sealed record ObterFaixaCatalogoQuery(string ExternalId) : IRequest<Result<FaixaCatalogoDto>>;

public sealed class ObterAlbumCatalogoQueryHandler(IMusicCatalogProvider provider)
    : IRequestHandler<ObterAlbumCatalogoQuery, Result<AlbumCatalogoDto>>
{
    public Task<Result<AlbumCatalogoDto>> Handle(ObterAlbumCatalogoQuery query, CancellationToken ct) =>
        !RegrasCatalogo.IdValido(query.ExternalId)
            ? Task.FromResult(ResultadosCatalogo.IdInvalido<AlbumCatalogoDto>())
            : ResultadosCatalogo.ConsultarAsync(() => provider.ObterAlbumAsync(query.ExternalId, ct));
}

public sealed class ObterFaixaCatalogoQueryHandler(IMusicCatalogProvider provider)
    : IRequestHandler<ObterFaixaCatalogoQuery, Result<FaixaCatalogoDto>>
{
    public Task<Result<FaixaCatalogoDto>> Handle(ObterFaixaCatalogoQuery query, CancellationToken ct) =>
        !RegrasCatalogo.IdValido(query.ExternalId)
            ? Task.FromResult(ResultadosCatalogo.IdInvalido<FaixaCatalogoDto>())
            : ResultadosCatalogo.ConsultarAsync(() => provider.ObterFaixaAsync(query.ExternalId, ct));
}

internal static class ResultadosCatalogo
{
    public static Result<T> IdInvalido<T>() => Result.Fail<T>(Erros.Criar(TipoErro.Validacao,
        "Identificador deve ter de 1 a 128 caracteres: letras, números, hífen ou sublinhado.", "externalId"));

    public static async Task<Result<T>> ConsultarAsync<T>(Func<Task<T?>> consultar) where T : class
    {
        try
        {
            var item = await consultar();
            return item is null
                ? Result.Fail<T>(Erros.Criar(TipoErro.NaoEncontrado, "Item de catálogo não encontrado."))
                : Result.Ok(item);
        }
        catch (CatalogoIndisponivelException)
        {
            return Result.Fail<T>(Erros.Criar(TipoErro.Indisponivel, "Catálogo temporariamente indisponível."));
        }
    }
}
