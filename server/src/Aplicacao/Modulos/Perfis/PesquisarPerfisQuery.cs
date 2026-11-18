using FluentResults;
using MediatR;
using Somnia.Aplicacao.Compartilhado;
using Somnia.Dominio.Compartilhado;
using Somnia.Dominio.Modulos.Perfis;

namespace Somnia.Aplicacao.Modulos.Perfis;

public sealed record PesquisarPerfisQuery(Guid UsuarioId, string? Q, int Limit = 10, int Offset = 0)
    : IRequest<Result<PaginaBuscaPerfisDto>>;

public sealed class PesquisarPerfisQueryHandler(IRepositorioPerfil repositorio)
    : IRequestHandler<PesquisarPerfisQuery, Result<PaginaBuscaPerfisDto>>
{
    public Task<Result<PaginaBuscaPerfisDto>> Handle(PesquisarPerfisQuery query, CancellationToken ct)
    {
        var termo = query.Q?.Trim();
        var erros = RegrasBuscaPerfis.Validar(termo, query.Limit, query.Offset).ToArray();
        if (erros.Length > 0)
            return Task.FromResult(Result.Fail<PaginaBuscaPerfisDto>(Erros.Validacao(erros)));

        return BuscarAsync(query.UsuarioId, termo!, query.Limit, query.Offset, ct);
    }

    private async Task<Result<PaginaBuscaPerfisDto>> BuscarAsync(
        Guid usuarioId, string termo, int limit, int offset, CancellationToken ct) =>
        Result.Ok(await repositorio.PesquisarAsync(usuarioId, termo, limit, offset, ct));
}
