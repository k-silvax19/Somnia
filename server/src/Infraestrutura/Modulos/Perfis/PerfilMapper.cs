using Somnia.Dominio.Compartilhado;
using Somnia.Dominio.Modulos.Perfis;
using Somnia.Infraestrutura.Compartilhado.Orm;

namespace Somnia.Infraestrutura.Modulos.Perfis;

public static class PerfilMapper
{
    public static UsuarioDto Privado(SomniaUser usuario)
    {
        return new UsuarioDto(
            usuario.Id,
            usuario.Nome,
            usuario.Email!,
            usuario.UserName,
            usuario.Bio,
            usuario.AvatarUrl);
    }
}
