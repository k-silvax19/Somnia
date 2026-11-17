using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Somnia.Infraestrutura.Compartilhado.Orm.Config;

public sealed class TokenRevogadoConfiguration : IEntityTypeConfiguration<TokenRevogado>
{
    public void Configure(EntityTypeBuilder<TokenRevogado> builder)
    {
        builder.HasKey(token => token.Jti);

        builder.Property(token => token.Jti)
            .HasMaxLength(64);

        builder.HasIndex(token => token.ExpiraEmUtc);
    }
}
