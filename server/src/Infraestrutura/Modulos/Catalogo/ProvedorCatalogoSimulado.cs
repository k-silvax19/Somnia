using Somnia.Dominio.Modulos.Catalogo;

namespace Somnia.Infraestrutura.Modulos.Catalogo;

public sealed class ProvedorCatalogoSimulado : IMusicCatalogProvider
{
    // Dados fictícios: IDs não pertencem ao Spotify e não recebem links/capas inventados.
    private static readonly IReadOnlyList<AlbumCatalogoDto> Albuns = Array.AsReadOnly(new[]
    {
        CriarAlbum("somnia-demo-album-aurora", "Aurora",
            ("somnia-demo-track-primeira-luz", "Primeira Luz"),
            ("somnia-demo-track-horizonte", "Horizonte"),
            ("somnia-demo-track-amanhecer", "Amanhecer")),
        CriarAlbum("somnia-demo-album-noturno", "Noturno",
            ("somnia-demo-track-lua", "Lua"),
            ("somnia-demo-track-silencio", "Silêncio"),
            ("somnia-demo-track-madrugada", "Madrugada"))
    });

    public Task<PaginaCatalogoDto> PesquisarAsync(string texto, TipoItemCatalogo tipo, int limit, int offset, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        IEnumerable<ItemCatalogoDto> itens = tipo switch
        {
            TipoItemCatalogo.Album => Albuns.Select(Resumo),
            TipoItemCatalogo.Faixa => Albuns.SelectMany(x => x.Tracks).Select(Resumo),
            _ => throw new ArgumentOutOfRangeException(nameof(tipo))
        };
        var encontrados = itens.Where(x => x.Title.Contains(texto, StringComparison.OrdinalIgnoreCase)
            || x.Artists.Any(artist => artist.Contains(texto, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(x => x.Title, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.ExternalId, StringComparer.Ordinal).ToArray();
        var pagina = Array.AsReadOnly(encontrados.Skip(offset).Take(limit).ToArray());
        return Task.FromResult(new PaginaCatalogoDto(pagina, limit, offset, encontrados.Length));
    }

    public Task<AlbumCatalogoDto?> ObterAlbumAsync(string externalId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(Albuns.SingleOrDefault(x => x.ExternalId == externalId));
    }

    public Task<FaixaCatalogoDto?> ObterFaixaAsync(string externalId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        foreach (var album in Albuns)
        {
            var faixa = album.Tracks.SingleOrDefault(x => x.ExternalId == externalId);
            if (faixa is not null)
                return Task.FromResult<FaixaCatalogoDto?>(new FaixaCatalogoDto(faixa, Resumo(album), faixa.TrackNumber));
        }
        return Task.FromResult<FaixaCatalogoDto?>(null);
    }

    private static AlbumCatalogoDto CriarAlbum(string id, string titulo, params (string Id, string Titulo)[] faixas)
    {
        var artistas = Array.AsReadOnly(new[] { "Somnia Demo" });
        var album = new ItemCatalogoDto("simulated", "album", id, titulo, artistas, null, null);
        var tracks = faixas.Select((faixa, indice) => new FaixaAlbumCatalogoDto(
            new ItemCatalogoDto("simulated", "track", faixa.Id, faixa.Titulo, artistas, null, null), indice + 1)).ToArray();
        return new AlbumCatalogoDto(album, Array.AsReadOnly(tracks));
    }

    private static ItemCatalogoDto Resumo(ItemCatalogoDto item) => new(
        item.Provider, item.Type, item.ExternalId, item.Title, item.Artists, item.CoverUrl, item.SpotifyUrl);
}
