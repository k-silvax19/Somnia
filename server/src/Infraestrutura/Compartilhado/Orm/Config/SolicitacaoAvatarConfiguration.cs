using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Somnia.Infraestrutura.Compartilhado.Orm.Config;

public sealed class SolicitacaoAvatarConfiguration : IEntityTypeConfiguration<SolicitacaoAvatar>
{
    public void Configure(EntityTypeBuilder<SolicitacaoAvatar> builder)
    {
        builder.ToTable("SolicitacoesAvatar");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Status).HasMaxLength(16);
        builder.Property(x => x.Erro).HasMaxLength(300);
        builder.HasIndex(x => new { x.UsuarioId, x.Status });
        builder.HasIndex(x => new { x.Status, x.CriadoEmUtc });
        builder.HasOne<SomniaUser>().WithMany().HasForeignKey(x => x.UsuarioId).OnDelete(DeleteBehavior.Cascade);
    }
}
