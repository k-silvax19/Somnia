using FluentResults;
using MediatR;
using Somnia.Aplicacao.Compartilhado;
using Somnia.Dominio.Compartilhado;

namespace Somnia.Aplicacao.Modulos.Auth;

public sealed record ObterUsuarioQuery(Guid UsuarioId) : IRequest<Result<UsuarioDto>>;

public sealed class ObterUsuarioQueryHandler(IGerenciadorIdentidade identidade)
    : IRequestHandler<ObterUsuarioQuery, Result<UsuarioDto>>
{
    public async Task<Result<UsuarioDto>> Handle(ObterUsuarioQuery query, CancellationToken ct)
    {
        var usuario = await identidade.ObterPorIdAsync(query.UsuarioId, ct);
        if (usuario is null)
        {
            return Result.Fail<UsuarioDto>(Erros.Criar(TipoErro.NaoEncontrado, "Usuário não encontrado."));
        }

        return Result.Ok(usuario);
    }
}
