using FluentResults;
using MediatR;
using Somnia.Aplicacao.Compartilhado;
using Somnia.Dominio.Compartilhado;

namespace Somnia.Aplicacao.Modulos.Auth;

public sealed record AutenticarUsuarioCommand(string? Email, string? Senha)
    : IRequest<Result<TokenDto>>;

public sealed class AutenticarUsuarioCommandHandler(
    IGerenciadorIdentidade identidade,
    IEmissorTokens emissor,
    IRefreshTokens refreshTokens)
    : IRequestHandler<AutenticarUsuarioCommand, Result<TokenDto>>
{
    public async Task<Result<TokenDto>> Handle(AutenticarUsuarioCommand command, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(command.Email) || string.IsNullOrWhiteSpace(command.Senha))
        {
            return Result.Fail<TokenDto>(
                Erros.Criar(TipoErro.Validacao, "E-mail e senha são obrigatórios.", "credenciais"));
        }

        var usuario = await identidade.AutenticarAsync(command.Email, command.Senha, ct);
        if (usuario is null)
        {
            return Result.Fail<TokenDto>(Erros.Criar(TipoErro.NaoAutenticado, "Credenciais inválidas."));
        }

        var refreshToken = await refreshTokens.CriarAsync(usuario.Id, ct);
        var token = emissor.CriarToken(usuario, refreshToken.SessaoId) with
        {
            RefreshToken = refreshToken.Token,
            RefreshExpiraEmUtc = refreshToken.ExpiraEmUtc
        };

        return Result.Ok(token);
    }
}
