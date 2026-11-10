using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Somnia.Dominio.Modulos.Catalogo;

namespace Somnia.Infraestrutura.Modulos.Catalogo;

public static class CatalogoExtensions
{
    public static IServiceCollection AddMusicCatalogServices(this IServiceCollection services,
        IConfiguration configuration, bool desenvolvimento)
    {
        switch (configuration["MusicCatalog:Provider"] ?? "Disabled")
        {
            case "Simulated" when desenvolvimento:
                services.AddSingleton<IMusicCatalogProvider, ProvedorCatalogoSimulado>();
                break;
            case "Disabled":
                services.AddSingleton<IMusicCatalogProvider, ProvedorCatalogoIndisponivel>();
                break;
            default:
                throw new InvalidOperationException("MusicCatalog:Provider deve ser Disabled ou Simulated (somente em Development).");
        }
        return services;
    }
}

public sealed class ProvedorCatalogoIndisponivel : IMusicCatalogProvider
{
    public Task<PaginaCatalogoDto> PesquisarAsync(string texto, TipoItemCatalogo tipo, int limit, int offset, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        throw new CatalogoIndisponivelException();
    }

    public Task<AlbumCatalogoDto?> ObterAlbumAsync(string externalId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        throw new CatalogoIndisponivelException();
    }

    public Task<FaixaCatalogoDto?> ObterFaixaAsync(string externalId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        throw new CatalogoIndisponivelException();
    }
}
