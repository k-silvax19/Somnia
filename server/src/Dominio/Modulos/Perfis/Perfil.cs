using Somnia.Dominio.Compartilhado;

namespace Somnia.Dominio.Modulos.Perfis;

public sealed record PerfilPublicoDto(string Username, string DisplayName, string? Bio, string? AvatarUrl);

public sealed record AlteracaoPerfil(
    string? DisplayName,
    bool AlterarDisplayName,
    string? Bio,
    bool AlterarBio,
    string? DefaultScoreVisibility,
    bool AlterarScoreVisibility,
    string? DefaultReviewVisibility,
    bool AlterarReviewVisibility
);

public interface IRepositorioPerfil
{
    Task<PerfilPublicoDto?> ObterPublicoAsync(string username, CancellationToken ct);
    Task<UsuarioDto?> AtualizarAsync(Guid usuarioId, AlteracaoPerfil alteracao, CancellationToken ct);
}
