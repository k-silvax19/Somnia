using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Somnia.Infraestrutura.Compartilhado.Orm;

namespace Somnia.Api.Compartilhado.Orm;

public sealed class SomniaDbContextFactory : IDesignTimeDbContextFactory<SomniaDbContext>
{
    public SomniaDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development"}.json", optional: true)
            .AddUserSecrets<SomniaDbContextFactory>(optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connection = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("Connection string \"Postgres\" não configurada.");

        var options = new DbContextOptionsBuilder<SomniaDbContext>()
            .UseNpgsql(connection, postgres => postgres.EnableRetryOnFailure(
                maxRetryCount: 10,
                maxRetryDelay: TimeSpan.FromSeconds(30),
                errorCodesToAdd: null))
            .Options;

        return new SomniaDbContext(options);
    }
}
