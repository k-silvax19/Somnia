namespace Somnia.Dominio.Compartilhado;

public sealed record UsuarioDto(Guid Id, string Nome, string Email);
public sealed record TokenDto(
    Guid UsuarioId,
    string Nome,
    string Email,
    string AccessToken,
    DateTime DataExpiracaoEmUtc
);
public sealed record CredenciaisUsuario(Guid Id, string Nome, string Email);

public interface IGerenciadorIdentidade
{
    Task<UsuarioDto> CadastrarAsync(
        string nome,
        string email,
        string senha,
        CancellationToken cancellationToken
    );
    Task<CredenciaisUsuario?> AutenticarAsync(
        string email,
        string senha,
        CancellationToken cancellationToken
    );
    Task<UsuarioDto?> ObterPorIdAsync(Guid usuarioId, CancellationToken cancellationToken);
}

public interface IEmissorTokens
{
    TokenDto CriarToken(CredenciaisUsuario usuario);
}

public interface IRevogacaoToken
{
    Task RevogarAsync(string jti, DateTime expiracaoUtc, CancellationToken cancellationToken);
    Task<bool> EstaRevogadoAsync(string jti, CancellationToken cancellationToken);
}
