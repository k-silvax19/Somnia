namespace Somnia.Infraestrutura.Compartilhado.Orm;

public sealed class RefreshToken
{
    public Guid Id { get; set; }
    public Guid UsuarioId { get; set; }
    public Guid SessaoId { get; set; }
    public SessaoAutenticacao Sessao { get; set; } = null!;
    public string TokenHash { get; set; } = string.Empty;
    public DateTime ExpiraEmUtc { get; set; }
    public DateTime? RevogadoEmUtc { get; set; }
    public SomniaUser Usuario { get; set; } = null!;
}
