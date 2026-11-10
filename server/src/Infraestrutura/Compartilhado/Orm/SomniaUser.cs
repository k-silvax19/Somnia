using Microsoft.AspNetCore.Identity;
using Somnia.Dominio.Modulos.Perfis;

namespace Somnia.Infraestrutura.Compartilhado.Orm;

public sealed class SomniaUser : IdentityUser<Guid>
{
    public string Nome { get; set; } = string.Empty;
    public string? Bio { get; set; }
    public string? AvatarUrl { get; set; }
    public Guid? AvatarSolicitacaoAtualId { get; set; }
    public Visibilidade DefaultScoreVisibility { get; set; } = Visibilidade.Privada;
    public Visibilidade DefaultReviewVisibility { get; set; } = Visibilidade.Privada;
}
