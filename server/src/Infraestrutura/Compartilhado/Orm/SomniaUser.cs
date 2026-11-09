using Microsoft.AspNetCore.Identity;

namespace Somnia.Infraestrutura.Compartilhado.Orm;

public sealed class SomniaUser : IdentityUser<Guid>
{
    public string Nome { get; set; } = string.Empty;
}
