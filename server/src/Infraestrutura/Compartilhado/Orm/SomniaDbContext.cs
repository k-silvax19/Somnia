using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using MassTransit;
using Somnia.Dominio.Modulos.Avaliacoes;
using Somnia.Dominio.Modulos.Relacoes;

namespace Somnia.Infraestrutura.Compartilhado.Orm;

public sealed class SomniaDbContext(DbContextOptions<SomniaDbContext> options)
    : IdentityUserContext<SomniaUser, Guid>(options)
{
    public DbSet<SessaoAutenticacao> Sessoes => Set<SessaoAutenticacao>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<TokenRevogado> TokensRevogados => Set<TokenRevogado>();
    public DbSet<AvatarUsuario> Avatares => Set<AvatarUsuario>();
    public DbSet<SolicitacaoAvatar> SolicitacoesAvatar => Set<SolicitacaoAvatar>();
    public DbSet<Avaliacao> Avaliacoes => Set<Avaliacao>();
    public DbSet<RelacaoSeguir> RelacoesSeguir => Set<RelacaoSeguir>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SomniaDbContext).Assembly);
    }
}
