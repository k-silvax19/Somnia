using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Somnia.Dominio.Compartilhado;
using Somnia.Dominio.Modulos.Perfis;
using Somnia.Dominio.Modulos.Avaliacoes;
using Somnia.Infraestrutura.Modulos.Avaliacoes;
using Somnia.Dominio.Modulos.Relacoes;
using Somnia.Infraestrutura.Modulos.Relacoes;
using Somnia.Infraestrutura.Modulos.Perfis;
using Somnia.Infraestrutura.Compartilhado.Auth;
using Somnia.Infraestrutura.Compartilhado.Orm;

namespace Somnia.Infraestrutura;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        var connection = configuration.GetConnectionString("SqlServer");
        if (string.IsNullOrWhiteSpace(connection))
        {
            throw new InvalidOperationException("Connection string \"SqlServer\" não configurada.");
        }

        services.AddDbContext<SomniaDbContext>(options =>
            options.UseSqlServer(connection, sql => sql.EnableRetryOnFailure(3)));

        services.AddIdentityCore<SomniaUser>(options =>
        {
            options.User.RequireUniqueEmail = true;
            // Política do piloto: e-mail identifica a conta; confirmação não bloqueia login.
            options.SignIn.RequireConfirmedEmail = false;

            options.Password.RequiredLength = 8;
            options.Password.RequireDigit = true;
            options.Password.RequireNonAlphanumeric = true;
            options.Password.RequireUppercase = false;
            options.Password.RequireLowercase = false;

            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
            options.Lockout.AllowedForNewUsers = true;
        }).AddEntityFrameworkStores<SomniaDbContext>();

        services.AddScoped<IGerenciadorIdentidade, GerenciadorIdentidade>();
        services.AddScoped<IRevogacaoToken, RevogacaoTokenEmOrm>();
        services.AddScoped<IRefreshTokens, RefreshTokensEmOrm>();
        services.AddScoped<IRepositorioPerfil, RepositorioPerfilEmOrm>();
        services.AddScoped<IRepositorioAvatar, RepositorioAvatarEmOrm>();
        services.AddScoped<IRepositorioAvaliacao, RepositorioAvaliacaoEmOrm>();
        services.AddScoped<IRepositorioRelacao, RepositorioRelacaoEmOrm>();

        return services;
    }
}
