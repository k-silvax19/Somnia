using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Somnia.Dominio.Compartilhado;
using Somnia.Infraestrutura.Compartilhado.Orm;

namespace Somnia.Infraestrutura.Compartilhado.Auth;

public sealed class GerenciadorIdentidade(UserManager<SomniaUser> userManager)
    : IGerenciadorIdentidade
{
    public async Task<UsuarioDto> CadastrarAsync(
        string nome,
        string email,
        string senha,
        CancellationToken ct
    )
    {
        ct.ThrowIfCancellationRequested();

        var user = new SomniaUser
        {
            Id = Guid.CreateVersion7(),
            UserName = email,
            Email = email,
            Nome = nome.Trim()
        };
        var result = await userManager.CreateAsync(user, senha);

        if (result.Succeeded)
            return new UsuarioDto(user.Id, user.Nome, user.Email!);

        if (result.Errors.Any(x => x.Code is "DuplicateEmail" or "DuplicateUserName"))
            throw new InvalidOperationException("EMAIL_DUPLICADO");

        throw new ArgumentException(string.Join(" ", result.Errors.Select(x => x.Description)));
    }

    public async Task<CredenciaisUsuario?> AutenticarAsync(
        string email,
        string senha,
        CancellationToken ct
    )
    {
        ct.ThrowIfCancellationRequested();

        var user = await userManager.FindByEmailAsync(email);

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

        return new UsuarioDto(user.Id, user.Nome, user.Email!);
    }
}
