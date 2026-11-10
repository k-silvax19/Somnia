namespace Somnia.Dominio.Modulos.Catalogo;

public static class RegrasCatalogo
{
    public const int TextoMaximo = 200;
    public const int LimiteMaximo = 10;
    public const int OffsetMaximo = 1000;

    public static bool TryObterTipo(string? tipo, out TipoItemCatalogo resultado)
    {
        resultado = TipoItemCatalogo.Album;
        switch (tipo?.Trim().ToLowerInvariant())
        {
            case "album": return true;
            case "track": resultado = TipoItemCatalogo.Faixa; return true;
            default: return false;
        }
    }

    public static bool IdValido(string? id) => id is { Length: > 0 and <= 128 }
        && id.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');
}
