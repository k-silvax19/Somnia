using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Somnia.Dominio.Compartilhado;
using Somnia.Dominio.Modulos.Perfis;
using Somnia.Infraestrutura.Compartilhado.Orm;

namespace Somnia.Infraestrutura.Modulos.Perfis;

public sealed class RepositorioAvatarEmOrm(SomniaDbContext db, ILookupNormalizer normalizer) : IRepositorioAvatar
{
    public async Task<UsuarioDto?> SalvarAsync(Guid usuarioId, byte[]? imagem, CancellationToken ct)
    {
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transacao = await db.Database.BeginTransactionAsync(ct);
            // O UPDATE bloqueia a linha do titular até o commit, serializando uploads/remoções.
            var alterados = await db.Users.Where(x => x.Id == usuarioId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.AvatarUrl,
                    x => imagem == null ? null : "/api/users/" + x.UserName + "/avatar"), ct);
            if (alterados == 0)
                return null;

            await db.Avatares.Where(x => x.UsuarioId == usuarioId).ExecuteDeleteAsync(ct);
            if (imagem is not null)
            {
                // SQL parametrizado mantém a troca atômica e evita estado rastreado em retries.
                await db.Database.ExecuteSqlInterpolatedAsync(
                    $"INSERT INTO Avatares (UsuarioId, Conteudo) VALUES ({usuarioId}, {imagem})", ct);
            }

            var usuario = await db.Users.AsNoTracking().SingleAsync(x => x.Id == usuarioId, ct);
            await transacao.CommitAsync(ct);
            return PerfilMapper.Privado(usuario);
        });
    }

    public async Task<byte[]?> ObterAsync(string username, CancellationToken ct)
    {
        var normalizado = normalizer.NormalizeName(username.Trim());
        return await (from avatar in db.Avatares.AsNoTracking()
                      join usuario in db.Users on avatar.UsuarioId equals usuario.Id
                      where usuario.NormalizedUserName == normalizado
                      select avatar.Conteudo).SingleOrDefaultAsync(ct);
    }
}
