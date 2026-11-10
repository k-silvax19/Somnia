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
    Task<ProcessamentoAvatarDto?> SolicitarAsync(Guid usuarioId, byte[] arquivo, CancellationToken ct);
    Task<bool> RemoverAsync(Guid usuarioId, CancellationToken ct);
    Task<byte[]?> ObterAsync(string username, CancellationToken ct);
    Task<ProcessamentoAvatarDto?> ObterStatusAsync(Guid usuarioId, Guid solicitacaoId, CancellationToken ct);
    Task<byte[]?> IniciarProcessamentoAsync(Guid solicitacaoId, CancellationToken ct);
    Task FinalizarAsync(Guid solicitacaoId, byte[]? imagem, string? erro, CancellationToken ct);
    Task MarcarFalhaAsync(Guid solicitacaoId, CancellationToken ct);
}

public sealed record ProcessamentoAvatarDto(Guid Id, string Status, string? Erro);

public sealed class LimiteProcessamentosAvatarException : Exception;
