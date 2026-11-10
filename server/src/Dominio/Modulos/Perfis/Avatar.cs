using Somnia.Dominio.Compartilhado;

namespace Somnia.Dominio.Modulos.Perfis;

public static class LimitesAvatar
{
    public const int TamanhoMaximo = 2 * 1024 * 1024;
    public const int DimensaoMaxima = 2048;
    public const int DimensaoSaida = 512;
}

public interface IProcessadorAvatar
{
    // Retorna PNG estático normalizado; lança ArgumentException para arquivo inválido.
    byte[] Normalizar(byte[] arquivo);
}

public interface IRepositorioAvatar
{
    Task<UsuarioDto?> SalvarAsync(Guid usuarioId, byte[]? imagem, CancellationToken ct);
    Task<byte[]?> ObterAsync(string username, CancellationToken ct);
}
