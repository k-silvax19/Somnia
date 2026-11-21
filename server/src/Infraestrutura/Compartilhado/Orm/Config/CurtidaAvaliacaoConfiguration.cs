using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Somnia.Dominio.Modulos.Avaliacoes;

namespace Somnia.Infraestrutura.Compartilhado.Orm.Config;

public sealed class CurtidaAvaliacaoConfiguration : IEntityTypeConfiguration<CurtidaAvaliacao>
{
    public void Configure(EntityTypeBuilder<CurtidaAvaliacao> builder)
    {
        builder.ToTable("CurtidasAvaliacoes");
        builder.HasKey(x => new { x.UsuarioId, x.AvaliacaoId });
        builder.HasIndex(x => new { x.AvaliacaoId, x.CreatedAt, x.UsuarioId })
            .IsDescending(false, true, true);
        // A avaliação é removida junto das curtidas. A FK de usuário não tem
        // cascade para evitar caminhos múltiplos no SQL Server.
        builder.HasOne<SomniaUser>().WithMany().HasForeignKey(x => x.UsuarioId)
            .OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<Avaliacao>().WithMany().HasForeignKey(x => x.AvaliacaoId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
