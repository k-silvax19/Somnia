using Microsoft.EntityFrameworkCore;
using Somnia.Dominio.Modulos.Perfis;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Somnia.Infraestrutura.Compartilhado.Orm.Config;

public sealed class SomniaUserConfiguration : IEntityTypeConfiguration<SomniaUser>
{
    public void Configure(EntityTypeBuilder<SomniaUser> builder)
    {
        builder.Property(usuario => usuario.Nome)
            .IsRequired();

        builder.Property(usuario => usuario.Bio).HasMaxLength(300);
        builder.Property(usuario => usuario.AvatarUrl).HasMaxLength(2048);
        builder.Property(usuario => usuario.DefaultScoreVisibility).HasDefaultValue(Visibilidade.Privada);
        builder.Property(usuario => usuario.DefaultReviewVisibility).HasDefaultValue(Visibilidade.Privada);
    }
}
