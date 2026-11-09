using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Somnia.Infraestrutura.Compartilhado.Orm.Config;

public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.HasKey(token => token.Id);
        builder.Property(token => token.TokenHash).HasMaxLength(64).IsRequired();
        builder.HasIndex(token => token.TokenHash).IsUnique();
        builder.HasIndex(token => token.UsuarioId);
        builder.HasOne(token => token.Sessao)
            .WithMany()
            .HasForeignKey(token => token.SessaoId)
            .OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(token => token.Usuario)
            .WithMany()
            .HasForeignKey(token => token.UsuarioId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
