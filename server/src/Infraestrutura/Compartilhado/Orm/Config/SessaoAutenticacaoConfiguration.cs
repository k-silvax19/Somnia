using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Somnia.Infraestrutura.Compartilhado.Orm.Config;

public sealed class SessaoAutenticacaoConfiguration : IEntityTypeConfiguration<SessaoAutenticacao>
{
    public void Configure(EntityTypeBuilder<SessaoAutenticacao> builder)
    {
        builder.HasKey(sessao => sessao.Id);
        builder.HasIndex(sessao => sessao.UsuarioId);
        builder.HasOne(sessao => sessao.Usuario)
            .WithMany()
            .HasForeignKey(sessao => sessao.UsuarioId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
