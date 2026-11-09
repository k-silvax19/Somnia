namespace Somnia.Dominio.Compartilhado;

public sealed record RefreshTokenDto(string Token, DateTime ExpiraEmUtc, Guid SessaoId);
public sealed record SessaoRenovada(CredenciaisUsuario Usuario, RefreshTokenDto RefreshToken);

public interface IRefreshTokens
{
    Task<RefreshTokenDto> CriarAsync(Guid usuarioId, CancellationToken ct);
    Task<SessaoRenovada?> RenovarAsync(string token, CancellationToken ct);
    Task RevogarPorUsuarioAsync(Guid usuarioId, CancellationToken ct);
    Task<bool> SessaoEstaAtivaAsync(Guid sessaoId, Guid usuarioId, CancellationToken ct);
}
