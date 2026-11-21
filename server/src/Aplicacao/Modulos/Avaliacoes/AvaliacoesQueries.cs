using FluentResults;
using MediatR;
using Somnia.Aplicacao.Compartilhado;
using Somnia.Dominio.Compartilhado;
using Somnia.Dominio.Modulos.Avaliacoes;

namespace Somnia.Aplicacao.Modulos.Avaliacoes;

public sealed record ObterAvaliacaoQuery(Guid Id, Guid UsuarioId) : IRequest<Result<AvaliacaoDto>>
{
    public ObterAvaliacaoQuery(Guid id) : this(id, Guid.Empty) { }
}
public sealed record ListarAvaliacoesPerfilQuery(string Username, Guid UsuarioId, int Limit, int Offset)
    : IRequest<Result<PaginaAvaliacoesDto>>
{
    public ListarAvaliacoesPerfilQuery(string username, int limit, int offset)
        : this(username, Guid.Empty, limit, offset) { }
}
public sealed record ListarAvaliacoesItemQuery(ReferenciaItem Item, Guid UsuarioId, int Limit, int Offset,
    string Sort)
    : IRequest<Result<AvaliacoesItemDto>>
{
    public ListarAvaliacoesItemQuery(ReferenciaItem item, Guid usuarioId, int limit, int offset)
        : this(item, usuarioId, limit, offset, "recent") { }
}

public sealed class ObterAvaliacaoQueryHandler(IRepositorioAvaliacao repositorio)
    : IRequestHandler<ObterAvaliacaoQuery, Result<AvaliacaoDto>>
{
    public async Task<Result<AvaliacaoDto>> Handle(ObterAvaliacaoQuery query, CancellationToken ct)
    {
        var avaliacao = query.UsuarioId == Guid.Empty
            ? await repositorio.ObterAsync(query.Id, ct)
            : await repositorio.ObterAsync(query.Id, query.UsuarioId, ct);
        return avaliacao is null ? ResultadosAvaliacao.NaoEncontrada<AvaliacaoDto>() : Result.Ok(avaliacao);
    }
}

public sealed class ListarAvaliacoesPerfilQueryHandler(IRepositorioAvaliacao repositorio)
    : IRequestHandler<ListarAvaliacoesPerfilQuery, Result<PaginaAvaliacoesDto>>
{
    public async Task<Result<PaginaAvaliacoesDto>> Handle(ListarAvaliacoesPerfilQuery query, CancellationToken ct)
    {
        var erros = RegrasAvaliacao.ValidarPagina(query.Limit, query.Offset).ToArray();
        if (erros.Length > 0)
            return Result.Fail<PaginaAvaliacoesDto>(Erros.Validacao(erros));
        var pagina = query.UsuarioId == Guid.Empty
            ? await repositorio.ListarPerfilAsync(query.Username, query.Limit, query.Offset, ct)
            : await repositorio.ListarPerfilAsync(query.Username, query.UsuarioId, query.Limit, query.Offset, ct);
        return pagina is null
            ? Result.Fail<PaginaAvaliacoesDto>(Erros.Criar(TipoErro.NaoEncontrado, "Perfil inexistente."))
            : Result.Ok(pagina);
    }
}

public sealed class ListarAvaliacoesItemQueryHandler(IRepositorioAvaliacao repositorio)
    : IRequestHandler<ListarAvaliacoesItemQuery, Result<AvaliacoesItemDto>>
{
    public async Task<Result<AvaliacoesItemDto>> Handle(ListarAvaliacoesItemQuery query, CancellationToken ct)
    {
        var erros = RegrasAvaliacao.ValidarItem(query.Item)
            .Concat(RegrasAvaliacao.ValidarPagina(query.Limit, query.Offset))
            .Concat(RegrasAvaliacao.ValidarOrdenacao(query.Sort)).ToArray();
        if (erros.Length > 0)
            return Result.Fail<AvaliacoesItemDto>(Erros.Validacao(erros));
        var ordenacao = query.Sort == "popular" ? OrdenacaoAvaliacoes.MaisCurtidas : OrdenacaoAvaliacoes.Recentes;
        return Result.Ok(ordenacao == OrdenacaoAvaliacoes.Recentes
            ? await repositorio.ListarItemAsync(query.Item, query.UsuarioId, query.Limit, query.Offset, ct)
            : await repositorio.ListarItemAsync(query.Item, query.UsuarioId, query.Limit,
                query.Offset, ordenacao, ct));
    }
}
