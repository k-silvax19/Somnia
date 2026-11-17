namespace Somnia.Infraestrutura.Compartilhado.Orm;

public sealed class AvatarUsuario
{
    public Guid UsuarioId { get; set; }
    public byte[] Conteudo { get; set; } = [];
}
