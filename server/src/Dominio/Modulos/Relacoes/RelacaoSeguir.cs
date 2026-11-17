using Somnia.Dominio.Compartilhado;

namespace Somnia.Dominio.Modulos.Relacoes;

public sealed class RelacaoSeguir
{
    private RelacaoSeguir() { }

    public Guid SeguidorId { get; private set; }
    public Guid SeguidoId { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public static RelacaoSeguir Criar(Guid seguidorId, Guid seguidoId)
    {
        if (seguidorId == Guid.Empty || seguidoId == Guid.Empty || seguidorId == seguidoId)
            throw new ArgumentException("Seguidor e seguido devem ser usuários distintos e válidos.");
        return new RelacaoSeguir { SeguidorId = seguidorId, SeguidoId = seguidoId, CreatedAt = DateTime.UtcNow };
    }
}

public enum TipoListaRelacoes { Seguidores, Seguidos }
public sealed record ResumoRelacoesDto(bool IsFollowing, int FollowersCount, int FollowingCount);
public sealed record PerfilRelacaoDto(string Username, string DisplayName, string? Bio, string? AvatarUrl, DateTime FollowedAt);
public sealed record PaginaRelacoesDto(IReadOnlyList<PerfilRelacaoDto> Items, int Limit, int Offset, int Total)
{
    public bool HasMore => (long)Offset + Items.Count < Total;
}

public interface IRepositorioRelacao
{
    Task<Guid?> ObterUsuarioIdAsync(string username, CancellationToken ct);
    Task SeguirAsync(Guid seguidorId, Guid seguidoId, CancellationToken ct);
    Task DeixarDeSeguirAsync(Guid seguidorId, Guid seguidoId, CancellationToken ct);
    Task<ResumoRelacoesDto> ObterResumoAsync(Guid usuarioId, Guid visitanteId, CancellationToken ct);
    Task<PaginaRelacoesDto> ListarAsync(Guid usuarioId, TipoListaRelacoes tipo, int limit, int offset, CancellationToken ct);
}

public static class RegrasRelacao
{
    public const int LimiteMaximo = 50;
    public const int OffsetMaximo = 10000;

    public static IEnumerable<ErroValidacao> ValidarUsername(string? username)
    {
        if (!RegrasCadastro.UsernameValido(username))
            yield return new("username", "Username inválido: use de 3 a 30 caracteres ASCII permitidos.");
    }

    public static IEnumerable<ErroValidacao> ValidarPagina(int limit, int offset)
    {
        if (limit is < 1 or > LimiteMaximo)
            yield return new("limit", $"Limite deve ser de 1 a {LimiteMaximo}.");
        if (offset is < 0 or > OffsetMaximo)
            yield return new("offset", $"Offset deve ser de 0 a {OffsetMaximo}.");
    }
}
