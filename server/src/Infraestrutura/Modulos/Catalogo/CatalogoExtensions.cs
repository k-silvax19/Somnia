using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Net;
using Somnia.Dominio.Modulos.Catalogo;
using Somnia.Infraestrutura.Modulos.Catalogo.Spotify;

namespace Somnia.Infraestrutura.Modulos.Catalogo;

public static class CatalogoExtensions
{
    public static IServiceCollection AddMusicCatalogServices(this IServiceCollection services,
        IConfiguration configuration, bool desenvolvimento)
    {
        switch (configuration["MusicCatalog:Provider"] ?? "Disabled")
        {
            case "Spotify":
                services.AddOptions<SpotifyOptions>().Bind(configuration.GetSection("Spotify"))
                    .Validate(x => !string.IsNullOrWhiteSpace(x.ClientId) && x.ClientId.Length <= 1024
                        && !x.ClientId.Contains(':'), "Spotify:ClientId é obrigatório e deve ser válido.")
                    .Validate(x => !string.IsNullOrWhiteSpace(x.ClientSecret) && x.ClientSecret.Length <= 1024,
                        "Spotify:ClientSecret é obrigatório.")
                    .Validate(x => x.Market is { Length: 2 } && x.Market.All(char.IsAsciiLetter),
                        "Spotify:Market deve ser um código ISO de duas letras, como BR.")
                    .Validate(x => x.TimeoutSeconds is >= 1 and <= 30, "Spotify:TimeoutSeconds deve estar entre 1 e 30.")
                    .ValidateOnStart();
                services.TryAddSingleton(TimeProvider.System);
                foreach (var (nome, endereco) in new[]
                {
                    (SpotifyClient.AuthClient, "https://accounts.spotify.com/"),
                    (SpotifyClient.ApiClient, "https://api.spotify.com/v1/")
                })
                    services.AddHttpClient(nome, client =>
                    {
                        client.BaseAddress = new Uri(endereco);
                        client.Timeout = Timeout.InfiniteTimeSpan;
                    }).ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
                    {
                        AllowAutoRedirect = false,
                        AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli,
                        PooledConnectionLifetime = TimeSpan.FromMinutes(5),
                        MaxConnectionsPerServer = 4,
                        MaxResponseHeadersLength = 32
                    }).RemoveAllLoggers();
                services.AddSingleton<SpotifyClient>();
                services.AddSingleton<IMusicCatalogProvider, ProvedorCatalogoSpotify>();
                break;
            case "Simulated" when desenvolvimento:
                services.AddSingleton<IMusicCatalogProvider, ProvedorCatalogoSimulado>();
                break;
            case "Disabled":
                services.AddSingleton<IMusicCatalogProvider, ProvedorCatalogoIndisponivel>();
                break;
            default:
                throw new InvalidOperationException("MusicCatalog:Provider deve ser Spotify, Disabled ou Simulated (somente em Development).");
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
