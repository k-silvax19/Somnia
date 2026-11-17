using FluentResults;
using MediatR;
using Somnia.Dominio.Compartilhado;

namespace Somnia.Aplicacao.Modulos.Auth;

public sealed record EncerrarSessaoCommand(string Jti, DateTime ExpiraEmUtc, Guid? UsuarioId = null) : IRequest<Result>;

public sealed class EncerrarSessaoCommandHandler(IRevogacaoToken revogacao, IRefreshTokens refreshTokens)
    : IRequestHandler<EncerrarSessaoCommand, Result>
{
    public async Task<Result> Handle(EncerrarSessaoCommand command, CancellationToken ct)
    {
        if (command.UsuarioId is not null)
            await refreshTokens.RevogarPorUsuarioAsync(command.UsuarioId.Value, ct);

        await revogacao.RevogarAsync(command.Jti, command.ExpiraEmUtc, ct);

        return Result.Ok();
    }
}
