using Microsoft.AspNetCore.Identity;

namespace Somnia.Infraestrutura.Compartilhado.Orm;

public sealed class SomniaUser : IdentityUser<Guid>
{
    public string Nome { get; set; } = string.Empty;
    public string? Bio { get; set; }
    public string? AvatarUrl { get; set; }
    public Guid? AvatarSolicitacaoAtualId { get; set; }
}
