using Microsoft.EntityFrameworkCore;
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
    }
}
