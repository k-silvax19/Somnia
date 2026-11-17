using FluentResults;
using MediatR;
using Somnia.Aplicacao.Compartilhado;
using Somnia.Dominio.Compartilhado;
using Somnia.Dominio.Modulos.Perfis;

namespace Somnia.Aplicacao.Modulos.Perfis;

public sealed record ObterPerfilPublicoQuery(string Username) : IRequest<Result<PerfilPublicoDto>>;

public sealed class ObterPerfilPublicoQueryHandler(IRepositorioPerfil repositorio)
    : IRequestHandler<ObterPerfilPublicoQuery, Result<PerfilPublicoDto>>
{
    public async Task<Result<PerfilPublicoDto>> Handle(ObterPerfilPublicoQuery query, CancellationToken ct)
    {
        var perfil = await repositorio.ObterPublicoAsync(query.Username, ct);
        if (perfil is null)
            return Result.Fail<PerfilPublicoDto>(Erros.Criar(TipoErro.NaoEncontrado, "Perfil inexistente."));

        return Result.Ok(perfil);
    }
}
