using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Somnia.Dominio.Compartilhado;
using Somnia.Dominio.Modulos.Perfis;
using Somnia.Infraestrutura.Compartilhado.Orm;

namespace Somnia.Infraestrutura.Modulos.Perfis;

public sealed class RepositorioPerfilEmOrm(SomniaDbContext db, ILookupNormalizer normalizer) : IRepositorioPerfil
{
    public async Task<PerfilPublicoDto?> ObterPublicoAsync(string username, CancellationToken ct)
    {
        var normalized = normalizer.NormalizeName(username.Trim());
        return await db.Users.AsNoTracking()
            .Where(usuario => usuario.NormalizedUserName == normalized)
            .Select(usuario => new PerfilPublicoDto(usuario.UserName!, usuario.Nome, usuario.Bio, usuario.AvatarUrl))
            .SingleOrDefaultAsync(ct);
    }

    public async Task<UsuarioDto?> AtualizarAsync(Guid usuarioId, AlteracaoPerfil alteracao, CancellationToken ct)
    {
        var usuario = await db.Users.SingleOrDefaultAsync(x => x.Id == usuarioId, ct);
        if (usuario is null)
            return null;

        if (alteracao.AlterarDisplayName)
            usuario.Nome = alteracao.DisplayName!.Trim();
        if (alteracao.AlterarBio)
            usuario.Bio = string.IsNullOrWhiteSpace(alteracao.Bio) ? null : alteracao.Bio.Trim();

        await db.SaveChangesAsync(ct);
        return PerfilMapper.Privado(usuario);
    }
}
