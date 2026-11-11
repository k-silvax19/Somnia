using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Somnia.Dominio.Modulos.Avaliacoes;

namespace Somnia.Infraestrutura.Compartilhado.Orm.Config;

public sealed class AvaliacaoConfiguration : IEntityTypeConfiguration<Avaliacao>
{
    public void Configure(EntityTypeBuilder<Avaliacao> builder)
    {
        builder.ToTable("Avaliacoes", tabela =>
        {
            tabela.HasCheckConstraint("CK_Avaliacoes_Score", "[Score] IS NULL OR [Score] BETWEEN 0 AND 100");
            tabela.HasCheckConstraint("CK_Avaliacoes_Conteudo", "[Score] IS NOT NULL OR LEN(LTRIM(RTRIM([Review]))) > 0 AND [Review] IS NOT NULL");
            tabela.HasCheckConstraint("CK_Avaliacoes_Provider", "[Provider] IN ('spotify', 'simulated')");
            tabela.HasCheckConstraint("CK_Avaliacoes_Type", "[Type] IN ('album', 'track')");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Provider).HasMaxLength(32).IsRequired().UseCollation("Latin1_General_100_BIN2");
        builder.Property(x => x.Type).HasMaxLength(10).IsRequired().UseCollation("Latin1_General_100_BIN2");
        // IDs do Spotify são sensíveis a maiúsculas; a collation padrão do banco pode não ser.
        builder.Property(x => x.ExternalId).HasMaxLength(128).IsRequired().UseCollation("Latin1_General_100_BIN2");
        builder.Property(x => x.Review).HasMaxLength(RegrasAvaliacao.ResenhaMaxima);
        builder.HasIndex(x => new { x.UsuarioId, x.Provider, x.Type, x.ExternalId }).IsUnique();
        builder.HasIndex(x => new { x.UsuarioId, x.CreatedAt, x.Id }).IsDescending(false, true, true);
        builder.HasIndex(x => new { x.Provider, x.Type, x.ExternalId, x.CreatedAt, x.Id });
        builder.HasOne<SomniaUser>().WithMany().HasForeignKey(x => x.UsuarioId).OnDelete(DeleteBehavior.Cascade);
    }
}
