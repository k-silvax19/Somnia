namespace Somnia.Infraestrutura.Modulos.Catalogo.Spotify;

public sealed class SpotifyOptions
{
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string Market { get; set; } = "BR";
    public int TimeoutSeconds { get; set; } = 10;
}
