using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Somnia.Dominio.Compartilhado;
using Somnia.Infraestrutura.Compartilhado.Orm;
using Somnia.Infraestrutura.Modulos.Perfis;

namespace Somnia.Infraestrutura.Compartilhado.Auth;

public sealed class GerenciadorIdentidade(UserManager<SomniaUser> userManager)
    : IGerenciadorIdentidade
{
    public async Task<UsuarioDto> CadastrarComUsernameAsync(
        string nome,
        string email,
        string senha,
        string username,
        CancellationToken ct
    )
    {
        ct.ThrowIfCancellationRequested();

        if (!RegrasCadastro.UsernameValido(username))
            throw new ArgumentException("Username deve ter de 3 a 30 caracteres e usar somente letras sem acento, números, ponto, hífen ou sublinhado.");

        var user = new SomniaUser
        {
            Id = Guid.CreateVersion7(),
            UserName = username.Trim(),
            Email = email.Trim(),
            Nome = nome.Trim()
        };
        var result = await userManager.CreateAsync(user, senha);

        if (result.Succeeded)
            return PerfilMapper.Privado(user);

        if (result.Errors.Any(x => x.Code == "DuplicateEmail"))
            throw new InvalidOperationException("EMAIL_DUPLICADO");

        if (result.Errors.Any(x => x.Code == "DuplicateUserName"))
            throw new InvalidOperationException("USERNAME_DUPLICADO");

        throw new ArgumentException(string.Join(" ", result.Errors.Select(x => x.Description)));
    }

    public async Task<CredenciaisUsuario?> AutenticarAsync(
        string email,
        string senha,
        CancellationToken ct
    )
    {
        ct.ThrowIfCancellationRequested();

        var user = await userManager.FindByEmailAsync(email.Trim());

        if (user is null || await userManager.IsLockedOutAsync(user))
            return null;

        if (!await userManager.CheckPasswordAsync(user, senha))
        {
            await userManager.AccessFailedAsync(user);
            return null;
        }

        await userManager.ResetAccessFailedCountAsync(user);

        return new CredenciaisUsuario(user.Id, user.Nome, user.Email!);
    }

    public async Task<UsuarioDto?> ObterPorIdAsync(Guid usuarioId, CancellationToken ct)
    {
        var user = await userManager.Users.FirstOrDefaultAsync(x => x.Id == usuarioId, ct);

        if (user is null)
            return null;

        return PerfilMapper.Privado(user);
    }
}
