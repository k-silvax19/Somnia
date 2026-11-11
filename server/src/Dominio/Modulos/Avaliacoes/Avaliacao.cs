using Somnia.Dominio.Compartilhado;
using Somnia.Dominio.Modulos.Catalogo;

namespace Somnia.Dominio.Modulos.Avaliacoes;

public sealed class Avaliacao
{
    private Avaliacao() { }

    public Guid Id { get; private set; }
    public Guid UsuarioId { get; private set; }
    public string Provider { get; private set; } = "";
    public string Type { get; private set; } = "";
    public string ExternalId { get; private set; } = "";
    public int? Score { get; private set; }
    public string? Review { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    public static Avaliacao Criar(Guid usuarioId, ReferenciaItem item, int? score, string? review)
    {
        if (usuarioId == Guid.Empty || RegrasAvaliacao.ValidarItem(item).Any()
            || RegrasAvaliacao.ValidarConteudo(score, review).Any())
            throw new ArgumentException("Avaliação inválida.");

        return new Avaliacao
        {
            Id = Guid.CreateVersion7(), UsuarioId = usuarioId,
            Provider = item.Provider, Type = item.Type, ExternalId = item.ExternalId,
            Score = score, Review = RegrasAvaliacao.NormalizarResenha(review), CreatedAt = DateTime.UtcNow
        };
    }
}

public sealed record ReferenciaItem(string Provider, string Type, string ExternalId);
public sealed record AutorAvaliacaoDto(string Username, string DisplayName);
public sealed record AvaliacaoDto(Guid Id, AutorAvaliacaoDto Author, ReferenciaItem Item,
    int? Score, string? Review, DateTime CreatedAt, DateTime? UpdatedAt);
public sealed record PaginaAvaliacoesDto(IReadOnlyList<AvaliacaoDto> Items, int Limit, int Offset, int Total)
{
    public bool HasMore => (long)Offset + Items.Count < Total;
}
public sealed record ResumoAvaliacoesDto(double? Average, int Count);
public sealed record AvaliacoesItemDto(PaginaAvaliacoesDto Reviews, ResumoAvaliacoesDto Summary, AvaliacaoDto? MyReview);

public interface IRepositorioAvaliacao
{
    Task<bool> ExisteAsync(Guid usuarioId, ReferenciaItem item, CancellationToken ct);
    Task<bool> CriarAsync(Avaliacao avaliacao, CancellationToken ct);
    Task<AvaliacaoDto?> ObterAsync(Guid id, CancellationToken ct);
    Task<AvaliacaoDto?> AtualizarAsync(Guid id, Guid usuarioId, int? score, string? review, CancellationToken ct);
    Task<bool> ExcluirAsync(Guid id, Guid usuarioId, CancellationToken ct);
    Task<PaginaAvaliacoesDto?> ListarPerfilAsync(string username, int limit, int offset, CancellationToken ct);
    Task<AvaliacoesItemDto> ListarItemAsync(ReferenciaItem item, Guid usuarioId, int limit, int offset, CancellationToken ct);
    Task<PaginaAvaliacoesDto> ListarFeedAsync(Guid usuarioId, int limit, int offset, CancellationToken ct);
    Task<PaginaAvaliacoesDto> ListarDescobertaAsync(int limit, int offset, CancellationToken ct);
}

public static class RegrasAvaliacao
{
    public const int ResenhaMaxima = 5000;
    public const int LimiteMaximo = 50;
    public static string? NormalizarResenha(string? review) => string.IsNullOrWhiteSpace(review) ? null : review.Trim();

    public static IEnumerable<ErroValidacao> ValidarConteudo(int? score, string? review)
    {
        if (score is < 0 or > 100)
            yield return new("score", "Nota deve ser um inteiro de 0 a 100.");
        if (review?.Length > ResenhaMaxima)
            yield return new("review", $"Resenha deve ter no máximo {ResenhaMaxima} caracteres.");
        if (score is null && string.IsNullOrWhiteSpace(review))
            yield return new("review", "Informe uma nota ou resenha.");
    }

    public static IEnumerable<ErroValidacao> ValidarItem(ReferenciaItem item)
    {
        if (item.Provider is not ("spotify" or "simulated"))
            yield return new("provider", "Provedor inválido.");
        if (item.Type is not ("album" or "track"))
            yield return new("type", "Use album ou track.");
        if (!RegrasCatalogo.IdValido(item.ExternalId))
            yield return new("externalId", "Identificador externo inválido.");
    }

    public static IEnumerable<ErroValidacao> ValidarPagina(int limit, int offset)
    {
        if (limit is < 1 or > LimiteMaximo)
            yield return new("limit", $"Limite deve ser de 1 a {LimiteMaximo}.");
        if (offset is < 0 or > 10000)
            yield return new("offset", "Offset deve ser de 0 a 10000.");
    }
}
