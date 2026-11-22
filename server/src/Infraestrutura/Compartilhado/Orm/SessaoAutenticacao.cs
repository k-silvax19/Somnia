namespace Somnia.Infraestrutura.Compartilhado.Orm;

public sealed class SessaoAutenticacao
{
    public Guid Id { get; set; }
    public Guid UsuarioId { get; set; }
    public bool LembrarLogin { get; set; }
    public DateTime ExpiraEmUtc { get; set; }
    public DateTime? RevogadaEmUtc { get; set; }
    public SomniaUser Usuario { get; set; } = null!;
}
