namespace Somnia.Dominio.Compartilhado;

public static class RegrasCadastro
{
    public const int UsernameMinimo = 3;
    public const int UsernameMaximo = 30;
    public const int NomeMaximo = 256;

    public static bool UsernameValido(string? username) =>
        username is { Length: >= UsernameMinimo and <= UsernameMaximo }
        && username.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '_');
}
