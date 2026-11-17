using FluentResults;
using MediatR;
using Somnia.Aplicacao.Compartilhado;
using Somnia.Dominio.Compartilhado;

namespace Somnia.Aplicacao.Modulos.Auth;

public sealed record RenovarSessaoCommand(string? RefreshToken) : IRequest<Result<TokenDto>>;

public sealed class RenovarSessaoCommandHandler(IRefreshTokens refreshTokens, IEmissorTokens emissor)
    : IRequestHandler<RenovarSessaoCommand, Result<TokenDto>>
{
    public async Task<Result<TokenDto>> Handle(RenovarSessaoCommand command, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(command.RefreshToken) || command.RefreshToken.Length != 128
            || command.RefreshToken.Any(caractere => !Uri.IsHexDigit(caractere)))
        {
            return Result.Fail<TokenDto>(Erros.Criar(TipoErro.Validacao, "Refresh token inválido.", "refreshToken"));
        }

        var sessao = await refreshTokens.RenovarAsync(command.RefreshToken.ToUpperInvariant(), ct);
        if (sessao is null)
        {
            return Result.Fail<TokenDto>(Erros.Criar(TipoErro.NaoAutenticado, "Sessão inválida ou expirada."));
        }

        var token = emissor.CriarToken(sessao.Usuario, sessao.RefreshToken.SessaoId) with
        {
            RefreshToken = sessao.RefreshToken.Token,
            RefreshExpiraEmUtc = sessao.RefreshToken.ExpiraEmUtc
        };

        return Result.Ok(token);
    }
}
