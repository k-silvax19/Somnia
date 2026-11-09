using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Somnia.Infraestrutura.Compartilhado.Orm;

public sealed class SomniaDbContext(DbContextOptions<SomniaDbContext> options)
    : IdentityUserContext<SomniaUser, Guid>(options)
{
    public DbSet<SessaoAutenticacao> Sessoes => Set<SessaoAutenticacao>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<TokenRevogado> TokensRevogados => Set<TokenRevogado>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SomniaDbContext).Assembly);
    }
}
