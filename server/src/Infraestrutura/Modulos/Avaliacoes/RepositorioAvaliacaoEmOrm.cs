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

    private IQueryable<AvaliacaoDto> Projetar(IQueryable<Avaliacao> consulta, Guid usuarioId) =>
        from avaliacao in consulta
        join usuario in db.Users on avaliacao.UsuarioId equals usuario.Id
        select new AvaliacaoDto(
            avaliacao.Id,
            new AutorAvaliacaoDto(usuario.UserName!, usuario.Nome, usuario.AvatarUrl),
            new ReferenciaItem(avaliacao.Provider, avaliacao.Type, avaliacao.ExternalId),
            avaliacao.Score,
            avaliacao.Review,
            avaliacao.CreatedAt,
            avaliacao.UpdatedAt,
            db.CurtidasAvaliacoes.Count(curtida => curtida.AvaliacaoId == avaliacao.Id),
            db.CurtidasAvaliacoes.Any(curtida => curtida.AvaliacaoId == avaliacao.Id && curtida.UsuarioId == usuarioId));

    public Task<AvaliacaoDto?> ObterAsync(Guid id, Guid usuarioId, CancellationToken ct) =>
        Projetar(db.Avaliacoes.AsNoTracking().Where(x => x.Id == id), usuarioId).SingleOrDefaultAsync(ct);

    public Task<AvaliacaoDto?> ObterAsync(Guid id, CancellationToken ct) => ObterAsync(id, Guid.Empty, ct);

    public async Task<AvaliacaoDto?> AtualizarAsync(Guid id, Guid usuarioId, int? score, string? review, CancellationToken ct)
    {
        var quantidade = await db.Avaliacoes.Where(x => x.Id == id && x.UsuarioId == usuarioId)
            .ExecuteUpdateAsync(set => set.SetProperty(x => x.Score, score)
                .SetProperty(x => x.Review, review).SetProperty(x => x.UpdatedAt, DateTime.UtcNow), ct);
        return quantidade == 0 ? null : await ObterAsync(id, usuarioId, ct);
    }

    public async Task<bool> ExcluirAsync(Guid id, Guid usuarioId, CancellationToken ct) =>
        await db.Avaliacoes.Where(x => x.Id == id && x.UsuarioId == usuarioId).ExecuteDeleteAsync(ct) > 0;

    public async Task<PaginaAvaliacoesDto?> ListarPerfilAsync(
        string username, Guid usuarioId, int limit, int offset, CancellationToken ct)
    {
        var normalized = normalizer.NormalizeName(username.Trim());
        var donoId = await db.Users.Where(x => x.NormalizedUserName == normalized)
            .Select(x => (Guid?)x.Id).SingleOrDefaultAsync(ct);
        return donoId is null ? null
            : await PaginarAsync(db.Avaliacoes.AsNoTracking().Where(x => x.UsuarioId == donoId),
                usuarioId, limit, offset, OrdenacaoAvaliacoes.Recentes, ct);
    }

    public Task<PaginaAvaliacoesDto?> ListarPerfilAsync(string username, int limit, int offset, CancellationToken ct) =>
        ListarPerfilAsync(username, Guid.Empty, limit, offset, ct);

    public async Task<AvaliacoesItemDto> ListarItemAsync(ReferenciaItem item, Guid usuarioId, int limit,
        int offset, OrdenacaoAvaliacoes ordenacao, CancellationToken ct)
    {
        var consulta = PorItem(item);
        var resumo = await consulta.Where(x => x.Score != null).GroupBy(x => 1)
            .Select(g => new { Average = g.Average(x => (double)x.Score!), Count = g.Count() }).SingleOrDefaultAsync(ct);
        return new AvaliacoesItemDto(await PaginarAsync(consulta, usuarioId, limit, offset, ordenacao, ct),
            new ResumoAvaliacoesDto(resumo is null ? null : Math.Round(resumo.Average, 1, MidpointRounding.AwayFromZero),
                resumo?.Count ?? 0),
            await Projetar(consulta.Where(x => x.UsuarioId == usuarioId), usuarioId).SingleOrDefaultAsync(ct));
    }

    public Task<AvaliacoesItemDto> ListarItemAsync(
        ReferenciaItem item, Guid usuarioId, int limit, int offset, CancellationToken ct) =>
        ListarItemAsync(item, usuarioId, limit, offset, OrdenacaoAvaliacoes.Recentes, ct);

    public Task<PaginaAvaliacoesDto> ListarFeedAsync(Guid usuarioId, int limit, int offset, CancellationToken ct) =>
        PaginarAsync(db.Avaliacoes.AsNoTracking().Where(avaliacao => db.RelacoesSeguir.Any(
            relacao => relacao.SeguidorId == usuarioId && relacao.SeguidoId == avaliacao.UsuarioId)),
            usuarioId, limit, offset, OrdenacaoAvaliacoes.Recentes, ct);

    public Task<PaginaAvaliacoesDto> ListarDescobertaAsync(Guid usuarioId, int limit, int offset, CancellationToken ct) =>
        PaginarAsync(db.Avaliacoes.AsNoTracking(), usuarioId, limit, offset, OrdenacaoAvaliacoes.Recentes, ct);

    public Task<PaginaAvaliacoesDto> ListarDescobertaAsync(int limit, int offset, CancellationToken ct) =>
        ListarDescobertaAsync(Guid.Empty, limit, offset, ct);

    public async Task<ResultadoCurtidaAvaliacao> AlterarCurtidaAsync(
        Guid avaliacaoId, Guid usuarioId, bool curtir, CancellationToken ct)
    {
        var avaliacao = await db.Avaliacoes.AsNoTracking()
            .Where(x => x.Id == avaliacaoId)
            .Select(x => new { x.UsuarioId })
            .SingleOrDefaultAsync(ct);
        if (avaliacao is null) return ResultadoCurtidaAvaliacao.AvaliacaoInexistente;
        if (curtir && avaliacao.UsuarioId == usuarioId) return ResultadoCurtidaAvaliacao.PropriaAvaliacao;

        if (!curtir)
        {
            await db.CurtidasAvaliacoes
                .Where(x => x.AvaliacaoId == avaliacaoId && x.UsuarioId == usuarioId)
                .ExecuteDeleteAsync(ct);
            return ResultadoCurtidaAvaliacao.Sucesso;
        }

        var curtida = CurtidaAvaliacao.Criar(usuarioId, avaliacaoId);
        db.CurtidasAvaliacoes.Add(curtida);
        try
        {
            await db.SaveChangesAsync(ct);
            return ResultadoCurtidaAvaliacao.Sucesso;
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            db.Entry(curtida).State = EntityState.Detached;
            return ResultadoCurtidaAvaliacao.Sucesso;
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 547 })
        {
            db.Entry(curtida).State = EntityState.Detached;
            return ResultadoCurtidaAvaliacao.AvaliacaoInexistente;
        }
    }

    public async Task<IReadOnlyList<RecomendacaoMusicalDto>> ListarRecomendacoesAsync(int limit, CancellationToken ct)
    {
        var ranking = await db.Avaliacoes.AsNoTracking()
            .Where(x => x.Score != null)
            .GroupBy(x => new { x.Provider, x.Type, x.ExternalId })
            .Select(group => new
            {
                group.Key.Provider,
                group.Key.Type,
                group.Key.ExternalId,
                AverageScore = group.Average(x => (double)x.Score!),
                ReviewCount = group.Count(),
            })
            .OrderByDescending(x => x.AverageScore)
            .ThenByDescending(x => x.ReviewCount)
            .Take(limit)
            .ToArrayAsync(ct);

        return ranking.Select(x => new RecomendacaoMusicalDto(
            new ReferenciaItem(x.Provider, x.Type, x.ExternalId), x.AverageScore, x.ReviewCount)).ToArray();
    }

    private async Task<PaginaAvaliacoesDto> PaginarAsync(IQueryable<Avaliacao> consulta, Guid usuarioId,
        int limit, int offset, OrdenacaoAvaliacoes ordenacao, CancellationToken ct)
    {
        var total = await consulta.CountAsync(ct);
        var pagina = Projetar(consulta, usuarioId);
        pagina = ordenacao == OrdenacaoAvaliacoes.MaisCurtidas
            ? pagina.OrderByDescending(x => x.LikesCount).ThenByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
            : pagina.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id);
        var items = await pagina.Skip(offset).Take(limit).ToArrayAsync(ct);
        return new PaginaAvaliacoesDto(items, limit, offset, total);
    }
}
