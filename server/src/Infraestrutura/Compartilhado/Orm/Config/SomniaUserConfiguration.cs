using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Somnia.Infraestrutura.Compartilhado.Orm.Config;

public sealed class SomniaUserConfiguration : IEntityTypeConfiguration<SomniaUser>
{
    public void Configure(EntityTypeBuilder<SomniaUser> builder)
    {
        builder.Property(usuario => usuario.Nome)
            .IsRequired();
    }
}
