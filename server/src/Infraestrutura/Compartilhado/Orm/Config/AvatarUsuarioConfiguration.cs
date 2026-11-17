using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Somnia.Infraestrutura.Compartilhado.Orm.Config;

public sealed class AvatarUsuarioConfiguration : IEntityTypeConfiguration<AvatarUsuario>
{
    public void Configure(EntityTypeBuilder<AvatarUsuario> builder)
    {
        builder.ToTable("Avatares");
        builder.HasKey(avatar => avatar.UsuarioId);
        builder.Property(avatar => avatar.Conteudo).IsRequired();
        builder.HasOne<SomniaUser>().WithOne().HasForeignKey<AvatarUsuario>(avatar => avatar.UsuarioId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
