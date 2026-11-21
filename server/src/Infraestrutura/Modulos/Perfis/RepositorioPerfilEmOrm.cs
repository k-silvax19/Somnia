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
        var termoLimpo = termo.Trim();
        var termoNormalizado = normalizer.NormalizeName(termoLimpo) ?? termoLimpo;
        var consulta = db.Users.AsNoTracking()
            .Where(usuario => usuario.Id != usuarioAtualId);

        // Search each word case- and accent-insensitively, as the former SQL collation did.
        foreach (var palavra in termoLimpo.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var palavraUsuario = $"%{EscaparLike(normalizer.NormalizeName(palavra) ?? palavra)}%";
            var palavraNome = $"%{EscaparLike(palavra)}%";
            consulta = consulta.Where(usuario =>
                EF.Functions.ILike(usuario.NormalizedUserName!, palavraUsuario, "~")
                || EF.Functions.ILike(
                    EF.Functions.Unaccent(usuario.Nome),
                    EF.Functions.Unaccent(palavraNome),
                    "~"));
        }

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
