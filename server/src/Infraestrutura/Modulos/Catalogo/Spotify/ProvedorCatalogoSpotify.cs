using System.Text.Json;
using Microsoft.Extensions.Options;
using Somnia.Dominio.Modulos.Catalogo;

namespace Somnia.Infraestrutura.Modulos.Catalogo.Spotify;

public sealed class ProvedorCatalogoSpotify(SpotifyClient client, IOptions<SpotifyOptions> options) : IMusicCatalogProvider
{
    private readonly string market = Uri.EscapeDataString(options.Value.Market.ToUpperInvariant());

    public async Task<PaginaCatalogoDto> PesquisarAsync(string texto, TipoItemCatalogo tipo, int limit, int offset, CancellationToken ct)
    {
        var type = tipo switch
        {
            TipoItemCatalogo.Album => "album",
            TipoItemCatalogo.Faixa => "track",
            _ => throw new ArgumentOutOfRangeException(nameof(tipo))
        };
        using var json = await client.ConsultarAsync(
            $"search?q={Uri.EscapeDataString(texto)}&type={type}&limit={limit}&offset={offset}&market={market}", false, ct)
            ?? throw new RespostaCatalogoInvalidaException();
        var page = Objeto(json.RootElement, type == "album" ? "albums" : "tracks");
        var total = Inteiro(page, "total");
        var items = Array(page, "items");
        if (items.GetArrayLength() > limit)
            throw new RespostaCatalogoInvalidaException();
        var resultado = items.EnumerateArray().Where(x => x.ValueKind != JsonValueKind.Null)
            .Select(x => Item(x, type)).ToArray();
        return new PaginaCatalogoDto(System.Array.AsReadOnly(resultado), limit, offset, total)
        {
            HasMore = items.GetArrayLength() > 0 && (long)offset + items.GetArrayLength() < total
        };
    }

    public async Task<AlbumCatalogoDto?> ObterAlbumAsync(string externalId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (!IdValido(externalId)) return null;
        using var prazo = CancellationTokenSource.CreateLinkedTokenSource(ct);
        prazo.CancelAfter(TimeSpan.FromSeconds(30));
        try
        {
            using var albumJson = await client.ConsultarAsync($"albums/{externalId}?market={market}", true, prazo.Token);
            if (albumJson is null) return null;
            var album = Item(albumJson.RootElement, "album");
            var faixas = new List<FaixaAlbumCatalogoDto>();
            var offset = 0;
            var total = 0;
            do
            {
                // Não segue URLs 'next' recebidas do provedor: host e caminho permanecem fixos.
                using var page = await client.ConsultarAsync(
                    $"albums/{externalId}/tracks?limit=50&offset={offset}&market={market}", false, prazo.Token)
                    ?? throw new RespostaCatalogoInvalidaException();
                total = Inteiro(page.RootElement, "total");
                if (total > 1000 || Inteiro(page.RootElement, "offset") != offset)
                    throw new RespostaCatalogoInvalidaException();
                var items = Array(page.RootElement, "items");
                var count = items.GetArrayLength();
                if (count > 50 || (count == 0 && offset < total))
                    throw new RespostaCatalogoInvalidaException();
                foreach (var faixa in items.EnumerateArray().Where(x => x.ValueKind != JsonValueKind.Null))
                {
                    var item = Item(faixa, "track") with { CoverUrl = album.CoverUrl };
                    faixas.Add(new FaixaAlbumCatalogoDto(item, Inteiro(faixa, "track_number", 1)));
                }
                offset += count;
            } while (offset < total);
            return new AlbumCatalogoDto(album, faixas.AsReadOnly());
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new CatalogoIndisponivelException();
        }
    }

    public async Task<FaixaCatalogoDto?> ObterFaixaAsync(string externalId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (!IdValido(externalId)) return null;
        using var json = await client.ConsultarAsync($"tracks/{externalId}?market={market}", true, ct);
        if (json is null) return null;
        var album = Item(Objeto(json.RootElement, "album"), "album");
        return new FaixaCatalogoDto(Item(json.RootElement, "track") with { CoverUrl = album.CoverUrl },
            album, Inteiro(json.RootElement, "track_number", 1));
    }

    private static ItemCatalogoDto Item(JsonElement json, string type)
    {
        var id = Texto(json, "id");
        if (!IdValido(id) || Texto(json, "type") != type)
            throw new RespostaCatalogoInvalidaException();
        var artistas = new List<string>();
        if (json.TryGetProperty("artists", out var artists) && artists.ValueKind == JsonValueKind.Array)
            foreach (var artist in artists.EnumerateArray())
            {
                var nome = Texto(artist, "name");
                if (!string.IsNullOrWhiteSpace(nome)) artistas.Add(nome);
            }
        string? capa = null;
        if (json.TryGetProperty("images", out var images) && images.ValueKind == JsonValueKind.Array)
            capa = images.EnumerateArray().Select(x => Texto(x, "url")).FirstOrDefault(UrlHttps);
        if (type == "track" && json.TryGetProperty("album", out var album) && album.ValueKind == JsonValueKind.Object)
        {
            if (album.TryGetProperty("images", out var albumImages) && albumImages.ValueKind == JsonValueKind.Array)
                capa = albumImages.EnumerateArray().Select(x => Texto(x, "url")).FirstOrDefault(UrlHttps);
        }
        var titulo = Texto(json, "name");
        return new ItemCatalogoDto("spotify", type, id!,
            string.IsNullOrWhiteSpace(titulo) ? "Título indisponível" : titulo,
            artistas.AsReadOnly(), capa, $"https://open.spotify.com/{type}/{id}");
    }

    private static bool IdValido(string? id) => id is { Length: 22 } && id.All(char.IsAsciiLetterOrDigit);
    private static bool UrlHttps(string? value) => Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && uri.Scheme == "https" && string.IsNullOrEmpty(uri.UserInfo);
    private static string? Texto(JsonElement json, string key) => json.ValueKind == JsonValueKind.Object
        && json.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    private static JsonElement Objeto(JsonElement json, string key) => json.TryGetProperty(key, out var value)
        && value.ValueKind == JsonValueKind.Object ? value : throw new RespostaCatalogoInvalidaException();
    private static JsonElement Array(JsonElement json, string key) => json.TryGetProperty(key, out var value)
        && value.ValueKind == JsonValueKind.Array ? value : throw new RespostaCatalogoInvalidaException();
    private static int Inteiro(JsonElement json, string key, int minimo = 0) => json.TryGetProperty(key, out var value)
        && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var numero) && numero >= minimo
        ? numero : throw new RespostaCatalogoInvalidaException();
}
