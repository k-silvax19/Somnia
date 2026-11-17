using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Somnia.Dominio.Modulos.Relacoes;
using Somnia.Infraestrutura.Compartilhado.Orm;

namespace Somnia.Infraestrutura.Modulos.Relacoes;

public sealed class RepositorioRelacaoEmOrm(SomniaDbContext db, ILookupNormalizer normalizer) : IRepositorioRelacao
{
    public Task<Guid?> ObterUsuarioIdAsync(string username, CancellationToken ct)
    {
        var normalized = normalizer.NormalizeName(username);
        return db.Users.Where(x => x.NormalizedUserName == normalized).Select(x => (Guid?)x.Id).SingleOrDefaultAsync(ct);
    }

    public async Task SeguirAsync(Guid seguidorId, Guid seguidoId, CancellationToken ct)
    {
        var relacao = RelacaoSeguir.Criar(seguidorId, seguidoId);
        db.RelacoesSeguir.Add(relacao);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            // PUT repetido é idempotente, inclusive em requisições simultâneas.
            db.Entry(relacao).State = EntityState.Detached;
        }
    }

    public async Task DeixarDeSeguirAsync(Guid seguidorId, Guid seguidoId, CancellationToken ct) =>
        await db.RelacoesSeguir.Where(x => x.SeguidorId == seguidorId && x.SeguidoId == seguidoId).ExecuteDeleteAsync(ct);

    public Task<ResumoRelacoesDto> ObterResumoAsync(Guid usuarioId, Guid visitanteId, CancellationToken ct) =>
        db.Users.Where(x => x.Id == usuarioId).Select(x => new ResumoRelacoesDto(
            db.RelacoesSeguir.Any(r => r.SeguidorId == visitanteId && r.SeguidoId == x.Id),
            db.RelacoesSeguir.Count(r => r.SeguidoId == x.Id),
            db.RelacoesSeguir.Count(r => r.SeguidorId == x.Id))).SingleAsync(ct);

    public async Task<PaginaRelacoesDto> ListarAsync(Guid usuarioId, TipoListaRelacoes tipo, int limit, int offset, CancellationToken ct)
    {
        var seguidores = tipo == TipoListaRelacoes.Seguidores;
        var consulta = db.RelacoesSeguir.AsNoTracking()
            .Where(x => seguidores ? x.SeguidoId == usuarioId : x.SeguidorId == usuarioId);
        var total = await consulta.CountAsync(ct);
        var pagina = from relacao in consulta
            join usuario in db.Users on (seguidores ? relacao.SeguidorId : relacao.SeguidoId) equals usuario.Id
            orderby relacao.CreatedAt descending, usuario.Id descending
            select new PerfilRelacaoDto(usuario.UserName!, usuario.Nome, usuario.Bio, usuario.AvatarUrl, relacao.CreatedAt);
        var items = await pagina.Skip(offset).Take(limit).ToArrayAsync(ct);
        return new PaginaRelacoesDto(items, limit, offset, total);
    }
}
