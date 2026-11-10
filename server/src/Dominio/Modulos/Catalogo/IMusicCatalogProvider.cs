namespace Somnia.Dominio.Modulos.Catalogo;

public enum TipoItemCatalogo { Album, Faixa }

public interface IMusicCatalogProvider
{
    Task<PaginaCatalogoDto> PesquisarAsync(string texto, TipoItemCatalogo tipo, int limit, int offset, CancellationToken ct);
    Task<AlbumCatalogoDto?> ObterAlbumAsync(string externalId, CancellationToken ct);
    Task<FaixaCatalogoDto?> ObterFaixaAsync(string externalId, CancellationToken ct);
}

public record ItemCatalogoDto(
    string Provider, string Type, string ExternalId, string Title,
    IReadOnlyList<string> Artists, string? CoverUrl, string? SpotifyUrl);

public sealed record FaixaAlbumCatalogoDto : ItemCatalogoDto
{
    public int TrackNumber { get; init; }

    public FaixaAlbumCatalogoDto(ItemCatalogoDto item, int trackNumber)
        : base(item.Provider, item.Type, item.ExternalId, item.Title, item.Artists, item.CoverUrl, item.SpotifyUrl)
    {
        TrackNumber = trackNumber;
    }
}

public sealed record AlbumCatalogoDto : ItemCatalogoDto
{
    public IReadOnlyList<FaixaAlbumCatalogoDto> Tracks { get; init; }

    public AlbumCatalogoDto(ItemCatalogoDto item, IReadOnlyList<FaixaAlbumCatalogoDto> tracks)
        : base(item.Provider, item.Type, item.ExternalId, item.Title, item.Artists, item.CoverUrl, item.SpotifyUrl)
    {
        Tracks = tracks;
    }
}

public sealed record FaixaCatalogoDto : ItemCatalogoDto
{
    public ItemCatalogoDto Album { get; init; }
    public int TrackNumber { get; init; }

    public FaixaCatalogoDto(ItemCatalogoDto item, ItemCatalogoDto album, int trackNumber)
        : base(item.Provider, item.Type, item.ExternalId, item.Title, item.Artists, item.CoverUrl, item.SpotifyUrl)
    {
        Album = album;
        TrackNumber = trackNumber;
    }
}

public sealed record PaginaCatalogoDto(IReadOnlyList<ItemCatalogoDto> Items, int Limit, int Offset, int Total)
{
    public bool HasMore => (long)Offset + Items.Count < Total;
}

public sealed class CatalogoIndisponivelException : Exception;
