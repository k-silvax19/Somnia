using FluentResults;
using MediatR;
using Somnia.Aplicacao.Compartilhado;
using Somnia.Dominio.Compartilhado;
using Somnia.Dominio.Modulos.Relacoes;

namespace Somnia.Aplicacao.Modulos.Relacoes;

public sealed record ObterResumoRelacoesQuery(Guid UsuarioId, string Username) : IRequest<Result<ResumoRelacoesDto>>;
public sealed record ListarRelacoesQuery(string Username, TipoListaRelacoes Tipo, int Limit, int Offset)
    : IRequest<Result<PaginaRelacoesDto>>;

public sealed class ObterResumoRelacoesQueryHandler(IRepositorioRelacao repositorio)
    : IRequestHandler<ObterResumoRelacoesQuery, Result<ResumoRelacoesDto>>
{
    public async Task<Result<ResumoRelacoesDto>> Handle(ObterResumoRelacoesQuery query, CancellationToken ct)
    {
        var erros = RegrasRelacao.ValidarUsername(query.Username).ToArray();
        if (erros.Length > 0)
            return Result.Fail<ResumoRelacoesDto>(Erros.Validacao(erros));
        var id = await repositorio.ObterUsuarioIdAsync(query.Username, ct);
        return id is null
            ? Result.Fail<ResumoRelacoesDto>(Erros.Criar(TipoErro.NaoEncontrado, "Perfil inexistente."))
            : Result.Ok(await repositorio.ObterResumoAsync(id.Value, query.UsuarioId, ct));
    }
}

public sealed class ListarRelacoesQueryHandler(IRepositorioRelacao repositorio)
    : IRequestHandler<ListarRelacoesQuery, Result<PaginaRelacoesDto>>
{
    public async Task<Result<PaginaRelacoesDto>> Handle(ListarRelacoesQuery query, CancellationToken ct)
    {
        var erros = RegrasRelacao.ValidarUsername(query.Username)
            .Concat(RegrasRelacao.ValidarPagina(query.Limit, query.Offset)).ToArray();
        if (erros.Length > 0)
            return Result.Fail<PaginaRelacoesDto>(Erros.Validacao(erros));
        var id = await repositorio.ObterUsuarioIdAsync(query.Username, ct);
        return id is null
            ? Result.Fail<PaginaRelacoesDto>(Erros.Criar(TipoErro.NaoEncontrado, "Perfil inexistente."))
            : Result.Ok(await repositorio.ListarAsync(id.Value, query.Tipo, query.Limit, query.Offset, ct));
    }
}
