using Somnia.Dominio.Compartilhado;

namespace Somnia.Dominio.Modulos.Perfis;

public sealed record PerfilPublicoDto(string Username, string DisplayName, string? Bio, string? AvatarUrl, string BannerTheme);
public sealed record PerfilBuscaDto(string Username, string DisplayName, string? Bio, string? AvatarUrl);
public sealed record PaginaBuscaPerfisDto(IReadOnlyList<PerfilBuscaDto> Items, int Limit, int Offset, int Total)
{
    public bool HasMore => (long)Offset + Items.Count < Total;
}

public sealed record AlteracaoPerfil(
    string? DisplayName,
    bool AlterarDisplayName,
    string? Bio,
    bool AlterarBio,
    string? BannerTheme,
    bool AlterarBannerTheme
);

public interface IRepositorioPerfil
{
    Task<PerfilPublicoDto?> ObterPublicoAsync(string username, CancellationToken ct);
    Task<PaginaBuscaPerfisDto> PesquisarAsync(Guid usuarioAtualId, string termo, int limit, int offset, CancellationToken ct);
    Task<UsuarioDto?> AtualizarAsync(Guid usuarioId, AlteracaoPerfil alteracao, CancellationToken ct);
}

public static class RegrasBuscaPerfis
{
    public const int TextoMaximo = 200;
    public const int LimiteMaximo = 10;
    public const int OffsetMaximo = 1000;

    public static IEnumerable<ErroValidacao> Validar(string? termo, int limit, int offset)
    {
        if (string.IsNullOrWhiteSpace(termo) || termo.Trim().Length > TextoMaximo)
            yield return new("q", $"Informe um termo de busca de 1 a {TextoMaximo} caracteres.");
        if (limit is < 1 or > LimiteMaximo)
            yield return new("limit", $"Limite deve ser de 1 a {LimiteMaximo}.");
        if (offset is < 0 or > OffsetMaximo)
            yield return new("offset", $"Offset deve ser de 0 a {OffsetMaximo}.");
    }
}
