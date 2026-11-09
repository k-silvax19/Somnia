using FluentResults;
using MediatR;
using Somnia.Dominio.Compartilhado;

namespace Somnia.Aplicacao.Modulos.Auth;

public sealed record EncerrarSessaoCommand(string Jti, DateTime ExpiraEmUtc) : IRequest<Result>;

public sealed class EncerrarSessaoCommandHandler(IRevogacaoToken revogacao)
    : IRequestHandler<EncerrarSessaoCommand, Result>
{
    public async Task<Result> Handle(EncerrarSessaoCommand command, CancellationToken ct)
    {
        await revogacao.RevogarAsync(command.Jti, command.ExpiraEmUtc, ct);

        return Result.Ok();
    }
}
