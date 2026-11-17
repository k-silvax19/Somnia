using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Somnia.Dominio.Compartilhado;
using Somnia.Infraestrutura.Compartilhado.Orm;

namespace Somnia.Infraestrutura.Compartilhado.Auth;

public sealed class RefreshTokensEmOrm(SomniaDbContext db) : IRefreshTokens
{
    public async Task<RefreshTokenDto> CriarAsync(Guid usuarioId, CancellationToken ct)
    {
        var sessao = new SessaoAutenticacao
        {
            Id = Guid.CreateVersion7(),
            UsuarioId = usuarioId,
            ExpiraEmUtc = DateTime.UtcNow.AddDays(30)
        };
        db.Sessoes.Add(sessao);
        var token = CriarRegistro(sessao);
        await db.SaveChangesAsync(ct);

        return token;
    }

    public async Task<SessaoRenovada?> RenovarAsync(string token, CancellationToken ct)
    {
        var hash = CalcularHash(token);

        return await db.Database.CreateExecutionStrategy().ExecuteAsync<SessaoRenovada?>(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            var registro = await db.RefreshTokens.AsNoTracking()
                .FirstOrDefaultAsync(x => x.TokenHash == hash, ct);

            if (registro is null)
                return null;

            // Serialize refresh and revocation for the same session, including concurrent replays.
            var sessao = await db.Sessoes
                .FromSqlInterpolated($"SELECT * FROM [Sessoes] WITH (UPDLOCK, ROWLOCK) WHERE [Id] = {registro.SessaoId}")
                .SingleOrDefaultAsync(ct);
            var agora = DateTime.UtcNow;

            if (sessao is null || sessao.RevogadaEmUtc is not null || sessao.ExpiraEmUtc <= agora)
                return null;

            registro = await db.RefreshTokens.AsNoTracking().SingleAsync(x => x.Id == registro.Id, ct);
            if (registro.RevogadoEmUtc is not null)
            {
                sessao.RevogadaEmUtc = agora;
                await db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
                return null;
            }

            if (registro.ExpiraEmUtc <= agora)
                return null;

            var usuario = await db.Users.AsNoTracking().SingleAsync(x => x.Id == sessao.UsuarioId, ct);
            if (usuario.LockoutEnd > agora)
                return null;

            await db.RefreshTokens.Where(x => x.Id == registro.Id)
                .ExecuteUpdateAsync(update => update.SetProperty(x => x.RevogadoEmUtc, agora), ct);
            var novoToken = CriarRegistro(sessao);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            return new SessaoRenovada(
                new CredenciaisUsuario(usuario.Id, usuario.Nome, usuario.Email!), novoToken);
        });
    }

    public async Task RevogarPorUsuarioAsync(Guid usuarioId, CancellationToken ct)
    {
        await db.Sessoes
            .Where(x => x.UsuarioId == usuarioId && x.RevogadaEmUtc == null)
            .ExecuteUpdateAsync(update => update.SetProperty(x => x.RevogadaEmUtc, DateTime.UtcNow), ct);
    }

    public async Task<bool> SessaoEstaAtivaAsync(Guid sessaoId, Guid usuarioId, CancellationToken ct)
    {
        var agora = DateTime.UtcNow;
        return await db.Sessoes.AnyAsync(x => x.Id == sessaoId && x.UsuarioId == usuarioId
            && x.RevogadaEmUtc == null && x.ExpiraEmUtc > agora, ct);
    }

    private RefreshTokenDto CriarRegistro(SessaoAutenticacao sessao)
    {
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(64));
        var prazo = DateTime.UtcNow.AddDays(7);
        var expiraEmUtc = DateTime.SpecifyKind(
            prazo < sessao.ExpiraEmUtc ? prazo : sessao.ExpiraEmUtc, DateTimeKind.Utc);
        db.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.CreateVersion7(),
            UsuarioId = sessao.UsuarioId,
            SessaoId = sessao.Id,
            TokenHash = CalcularHash(token),
            ExpiraEmUtc = expiraEmUtc
        });

        return new RefreshTokenDto(token, expiraEmUtc, sessao.Id);
    }

    private static string CalcularHash(string token)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }
}
