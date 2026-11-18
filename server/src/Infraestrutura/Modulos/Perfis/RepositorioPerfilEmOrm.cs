using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Somnia.Dominio.Compartilhado;
using Somnia.Dominio.Modulos.Perfis;
using Somnia.Infraestrutura.Compartilhado.Orm;

namespace Somnia.Infraestrutura.Modulos.Perfis;

public sealed class RepositorioPerfilEmOrm(SomniaDbContext db, ILookupNormalizer normalizer) : IRepositorioPerfil
{
    public async Task<PaginaBuscaPerfisDto> PesquisarAsync(
        Guid usuarioAtualId, string termo, int limit, int offset, CancellationToken ct)
    {
        var termoNormalizado = normalizer.NormalizeName(termo) ?? termo;
        var buscaUsuario = $"%{EscaparLike(termoNormalizado)}%";
        var buscaNome = $"%{EscaparLike(termo)}%";
        var consulta = db.Users.AsNoTracking()
            .Where(usuario => usuario.Id != usuarioAtualId
                && (EF.Functions.Like(usuario.NormalizedUserName!, buscaUsuario, "~")
                    || EF.Functions.Like(usuario.Nome, buscaNome, "~")));

        var total = await consulta.CountAsync(ct);
        var items = await consulta
            .OrderByDescending(usuario => usuario.NormalizedUserName == termoNormalizado)
            .ThenBy(usuario => usuario.NormalizedUserName)
            .ThenBy(usuario => usuario.Id)
            .Skip(offset)
            .Take(limit)
            .Select(usuario => new PerfilBuscaDto(
                usuario.UserName!, usuario.Nome, usuario.Bio, usuario.AvatarUrl))
            .ToListAsync(ct);

        return new PaginaBuscaPerfisDto(items, limit, offset, total);
    }

    public async Task<PerfilPublicoDto?> ObterPublicoAsync(string username, CancellationToken ct)
    {
        var normalized = normalizer.NormalizeName(username.Trim());
        return await db.Users.AsNoTracking()
            .Where(usuario => usuario.NormalizedUserName == normalized)
            .Select(usuario => new PerfilPublicoDto(usuario.UserName!, usuario.Nome, usuario.Bio, usuario.AvatarUrl, usuario.BannerTheme))
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
        if (alteracao.AlterarBannerTheme)
            usuario.BannerTheme = alteracao.BannerTheme!;

        await db.SaveChangesAsync(ct);
        return PerfilMapper.Privado(usuario);
    }

    private static string EscaparLike(string valor) => valor
        .Replace("~", "~~", StringComparison.Ordinal)
        .Replace("%", "~%", StringComparison.Ordinal)
        .Replace("_", "~_", StringComparison.Ordinal)
        .Replace("[", "~[", StringComparison.Ordinal);
}
