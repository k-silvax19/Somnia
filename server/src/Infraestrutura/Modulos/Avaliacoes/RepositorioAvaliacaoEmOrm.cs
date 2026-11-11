using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Somnia.Dominio.Modulos.Avaliacoes;
using Somnia.Infraestrutura.Compartilhado.Orm;

namespace Somnia.Infraestrutura.Modulos.Avaliacoes;

public sealed class RepositorioAvaliacaoEmOrm(SomniaDbContext db, ILookupNormalizer normalizer) : IRepositorioAvaliacao
{
    private IQueryable<Avaliacao> PorItem(ReferenciaItem item) => db.Avaliacoes.AsNoTracking()
        .Where(x => x.Provider == item.Provider && x.Type == item.Type && x.ExternalId == item.ExternalId);

    public Task<bool> ExisteAsync(Guid usuarioId, ReferenciaItem item, CancellationToken ct) =>
        PorItem(item).AnyAsync(x => x.UsuarioId == usuarioId, ct);

    public async Task<bool> CriarAsync(Avaliacao avaliacao, CancellationToken ct)
    {
        db.Avaliacoes.Add(avaliacao);
        try
        {
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            db.Entry(avaliacao).State = EntityState.Detached;
            return false;
        }
    }

    private IQueryable<AvaliacaoDto> Projetar(IQueryable<Avaliacao> consulta) =>
        from avaliacao in consulta
        join usuario in db.Users on avaliacao.UsuarioId equals usuario.Id
        select new AvaliacaoDto(avaliacao.Id, new AutorAvaliacaoDto(usuario.UserName!, usuario.Nome),
            new ReferenciaItem(avaliacao.Provider, avaliacao.Type, avaliacao.ExternalId),
            avaliacao.Score, avaliacao.Review, avaliacao.CreatedAt, avaliacao.UpdatedAt);

    public Task<AvaliacaoDto?> ObterAsync(Guid id, CancellationToken ct) =>
        Projetar(db.Avaliacoes.AsNoTracking().Where(x => x.Id == id)).SingleOrDefaultAsync(ct);

    public async Task<AvaliacaoDto?> AtualizarAsync(Guid id, Guid usuarioId, int? score, string? review, CancellationToken ct)
    {
        var quantidade = await db.Avaliacoes.Where(x => x.Id == id && x.UsuarioId == usuarioId)
            .ExecuteUpdateAsync(set => set.SetProperty(x => x.Score, score)
                .SetProperty(x => x.Review, review).SetProperty(x => x.UpdatedAt, DateTime.UtcNow), ct);
        return quantidade == 0 ? null : await ObterAsync(id, ct);
    }

    public async Task<bool> ExcluirAsync(Guid id, Guid usuarioId, CancellationToken ct) =>
        await db.Avaliacoes.Where(x => x.Id == id && x.UsuarioId == usuarioId).ExecuteDeleteAsync(ct) > 0;

    public async Task<PaginaAvaliacoesDto?> ListarPerfilAsync(string username, int limit, int offset, CancellationToken ct)
    {
        var normalized = normalizer.NormalizeName(username.Trim());
        var usuarioId = await db.Users.Where(x => x.NormalizedUserName == normalized)
            .Select(x => (Guid?)x.Id).SingleOrDefaultAsync(ct);
        return usuarioId is null ? null
            : await PaginarAsync(db.Avaliacoes.AsNoTracking().Where(x => x.UsuarioId == usuarioId), limit, offset, ct);
    }

    public async Task<AvaliacoesItemDto> ListarItemAsync(ReferenciaItem item, Guid usuarioId, int limit, int offset, CancellationToken ct)
    {
        var consulta = PorItem(item);
        var resumo = await consulta.Where(x => x.Score != null).GroupBy(x => 1)
            .Select(g => new { Average = g.Average(x => (double)x.Score!), Count = g.Count() }).SingleOrDefaultAsync(ct);
        return new AvaliacoesItemDto(await PaginarAsync(consulta, limit, offset, ct),
            new ResumoAvaliacoesDto(resumo is null ? null : Math.Round(resumo.Average, 1, MidpointRounding.AwayFromZero),
                resumo?.Count ?? 0),
            await Projetar(consulta.Where(x => x.UsuarioId == usuarioId)).SingleOrDefaultAsync(ct));
    }

    public Task<PaginaAvaliacoesDto> ListarFeedAsync(Guid usuarioId, int limit, int offset, CancellationToken ct) =>
        PaginarAsync(db.Avaliacoes.AsNoTracking().Where(avaliacao => db.RelacoesSeguir.Any(
            relacao => relacao.SeguidorId == usuarioId && relacao.SeguidoId == avaliacao.UsuarioId)), limit, offset, ct);

    public Task<PaginaAvaliacoesDto> ListarDescobertaAsync(int limit, int offset, CancellationToken ct) =>
        PaginarAsync(db.Avaliacoes.AsNoTracking(), limit, offset, ct);

    private async Task<PaginaAvaliacoesDto> PaginarAsync(IQueryable<Avaliacao> consulta, int limit, int offset, CancellationToken ct)
    {
        var total = await consulta.CountAsync(ct);
        var pagina = from avaliacao in consulta
            join usuario in db.Users on avaliacao.UsuarioId equals usuario.Id
            orderby avaliacao.CreatedAt descending, avaliacao.Id descending
            select new AvaliacaoDto(avaliacao.Id, new AutorAvaliacaoDto(usuario.UserName!, usuario.Nome),
                new ReferenciaItem(avaliacao.Provider, avaliacao.Type, avaliacao.ExternalId),
                avaliacao.Score, avaliacao.Review, avaliacao.CreatedAt, avaliacao.UpdatedAt);
        var items = await pagina.Skip(offset).Take(limit).ToArrayAsync(ct);
        return new PaginaAvaliacoesDto(items, limit, offset, total);
    }
}
