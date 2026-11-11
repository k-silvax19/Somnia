using FluentResults;
using MediatR;
using Somnia.Aplicacao.Compartilhado;
using Somnia.Dominio.Compartilhado;
using Somnia.Dominio.Modulos.Relacoes;

namespace Somnia.Aplicacao.Modulos.Relacoes;

public sealed record SeguirPerfilCommand(Guid UsuarioId, string Username) : IRequest<Result>;
public sealed record DeixarDeSeguirPerfilCommand(Guid UsuarioId, string Username) : IRequest<Result>;

public sealed class RelacoesCommandHandler(IRepositorioRelacao repositorio)
    : IRequestHandler<SeguirPerfilCommand, Result>, IRequestHandler<DeixarDeSeguirPerfilCommand, Result>
{
    public Task<Result> Handle(SeguirPerfilCommand command, CancellationToken ct) =>
        AlterarAsync(command.UsuarioId, command.Username, true, ct);

    public Task<Result> Handle(DeixarDeSeguirPerfilCommand command, CancellationToken ct) =>
        AlterarAsync(command.UsuarioId, command.Username, false, ct);

    private async Task<Result> AlterarAsync(Guid usuarioId, string username, bool seguir, CancellationToken ct)
    {
        var erros = RegrasRelacao.ValidarUsername(username).ToArray();
        if (erros.Length > 0)
            return Result.Fail(Erros.Validacao(erros));
        var seguidoId = await repositorio.ObterUsuarioIdAsync(username, ct);
        if (seguidoId is null)
            return Result.Fail(Erros.Criar(TipoErro.NaoEncontrado, "Perfil inexistente."));
        if (seguidoId == usuarioId)
            return Result.Fail(Erros.Criar(TipoErro.Validacao, "Não é possível seguir o próprio perfil.", "username"));

        if (seguir)
            await repositorio.SeguirAsync(usuarioId, seguidoId.Value, ct);
        else
            await repositorio.DeixarDeSeguirAsync(usuarioId, seguidoId.Value, ct);
        return Result.Ok();
    }
}
