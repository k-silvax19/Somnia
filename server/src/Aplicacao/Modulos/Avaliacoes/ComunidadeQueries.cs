using FluentResults;
using MediatR;
using Somnia.Aplicacao.Compartilhado;
using Somnia.Dominio.Modulos.Avaliacoes;

namespace Somnia.Aplicacao.Modulos.Avaliacoes;

public sealed record ListarFeedQuery(Guid UsuarioId, int Limit, int Offset) : IRequest<Result<PaginaAvaliacoesDto>>;
public sealed record ListarDescobertaQuery(int Limit, int Offset) : IRequest<Result<PaginaAvaliacoesDto>>;

public sealed class ListarFeedQueryHandler(IRepositorioAvaliacao repositorio)
    : IRequestHandler<ListarFeedQuery, Result<PaginaAvaliacoesDto>>
{
    public async Task<Result<PaginaAvaliacoesDto>> Handle(ListarFeedQuery query, CancellationToken ct)
    {
        var erros = RegrasAvaliacao.ValidarPagina(query.Limit, query.Offset).ToArray();
        if (erros.Length > 0)
            return Result.Fail<PaginaAvaliacoesDto>(Erros.Validacao(erros));
        return Result.Ok(await repositorio.ListarFeedAsync(query.UsuarioId, query.Limit, query.Offset, ct));
    }
}

public sealed class ListarDescobertaQueryHandler(IRepositorioAvaliacao repositorio)
    : IRequestHandler<ListarDescobertaQuery, Result<PaginaAvaliacoesDto>>
{
    public async Task<Result<PaginaAvaliacoesDto>> Handle(ListarDescobertaQuery query, CancellationToken ct)
    {
        var erros = RegrasAvaliacao.ValidarPagina(query.Limit, query.Offset).ToArray();
        if (erros.Length > 0)
            return Result.Fail<PaginaAvaliacoesDto>(Erros.Validacao(erros));
        return Result.Ok(await repositorio.ListarDescobertaAsync(query.Limit, query.Offset, ct));
    }
}
