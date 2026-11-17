using FluentResults;
using MediatR;
using Somnia.Aplicacao.Compartilhado;
using Somnia.Dominio.Compartilhado;
using Somnia.Dominio.Modulos.Catalogo;

namespace Somnia.Aplicacao.Modulos.Catalogo;

public sealed record PesquisarCatalogoQuery(string? Q, string? Type, int Limit = 10, int Offset = 0)
    : IRequest<Result<PaginaCatalogoDto>>;

public sealed class PesquisarCatalogoQueryHandler(IMusicCatalogProvider provider)
    : IRequestHandler<PesquisarCatalogoQuery, Result<PaginaCatalogoDto>>
{
    public Task<Result<PaginaCatalogoDto>> Handle(PesquisarCatalogoQuery query, CancellationToken ct)
    {
        var erros = new List<Error>();
        var texto = query.Q?.Trim();
        if (string.IsNullOrWhiteSpace(texto) || texto.Length > RegrasCatalogo.TextoMaximo)
            erros.Add(Erros.Criar(TipoErro.Validacao, "Informe uma busca de 1 a 200 caracteres.", "q"));
        if (!RegrasCatalogo.TryObterTipo(query.Type, out var tipo))
            erros.Add(Erros.Criar(TipoErro.Validacao, "Use album ou track.", "type"));
        if (query.Limit is < 1 or > RegrasCatalogo.LimiteMaximo)
            erros.Add(Erros.Criar(TipoErro.Validacao, "Limit deve estar entre 1 e 10.", "limit"));
        if (query.Offset is < 0 or > RegrasCatalogo.OffsetMaximo)
            erros.Add(Erros.Criar(TipoErro.Validacao, "Offset deve estar entre 0 e 1000.", "offset"));
        if (erros.Count > 0)
            return Task.FromResult(Result.Fail<PaginaCatalogoDto>(erros));

        return ResultadosCatalogo.ConsultarAsync<PaginaCatalogoDto>(
            async () => await provider.PesquisarAsync(texto!, tipo, query.Limit, query.Offset, ct));
    }
}
