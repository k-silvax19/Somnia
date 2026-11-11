using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Somnia.Dominio.Modulos.Relacoes;

namespace Somnia.Infraestrutura.Compartilhado.Orm.Config;

public sealed class RelacaoSeguirConfiguration : IEntityTypeConfiguration<RelacaoSeguir>
{
    public void Configure(EntityTypeBuilder<RelacaoSeguir> builder)
    {
        builder.ToTable("RelacoesSeguir", tabela =>
            tabela.HasCheckConstraint("CK_RelacoesSeguir_UsuariosDistintos", "[SeguidorId] <> [SeguidoId]"));
        builder.HasKey(x => new { x.SeguidorId, x.SeguidoId });
        builder.HasIndex(x => new { x.SeguidorId, x.CreatedAt, x.SeguidoId }).IsDescending(false, true, true);
        builder.HasIndex(x => new { x.SeguidoId, x.CreatedAt, x.SeguidorId }).IsDescending(false, true, true);
        // Duas FKs para Identity: evitar múltiplos caminhos de cascata no SQL Server.
        builder.HasOne<SomniaUser>().WithMany().HasForeignKey(x => x.SeguidorId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<SomniaUser>().WithMany().HasForeignKey(x => x.SeguidoId).OnDelete(DeleteBehavior.NoAction);
    }
}
