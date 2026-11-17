namespace Somnia.Infraestrutura.Compartilhado.Orm;

public sealed class SolicitacaoAvatar
{
    public Guid Id { get; set; }
    public Guid UsuarioId { get; set; }
    public byte[]? Arquivo { get; set; }
    public string Status { get; set; } = "pending";
    public string? Erro { get; set; }
    public int Tentativas { get; set; }
    public DateTime CriadoEmUtc { get; set; }
}
