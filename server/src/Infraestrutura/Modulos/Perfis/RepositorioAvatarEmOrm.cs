using MassTransit;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Somnia.Aplicacao.Modulos.Perfis.Mensageria;
using Somnia.Dominio.Modulos.Perfis;
using Somnia.Infraestrutura.Compartilhado.Orm;

namespace Somnia.Infraestrutura.Modulos.Perfis;

public sealed class RepositorioAvatarEmOrm(
    SomniaDbContext db, ILookupNormalizer normalizer, ISendEndpointProvider mensagens) : IRepositorioAvatar
{
    public async Task<ProcessamentoAvatarDto?> SolicitarAsync(Guid usuarioId, byte[] arquivo, CancellationToken ct)
    {
        var id = Guid.NewGuid();
        var solicitacao = new SolicitacaoAvatar
        {
            Id = id, UsuarioId = usuarioId, Arquivo = arquivo, CriadoEmUtc = DateTime.UtcNow
        };
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transacao = await db.Database.BeginTransactionAsync(ct);
            // Serializa a admissão para que o limite de 100 imagens pendentes seja global.
            await db.Database.ExecuteSqlRawAsync("""
                DECLARE @resultado int;
                EXEC @resultado = sys.sp_getapplock @Resource=N'Somnia:Avatares',
                    @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=10000;
                IF @resultado < 0 THROW 51000, 'Nao foi possivel reservar processamento de avatar.', 1;
                """, ct);

            var existente = await db.SolicitacoesAvatar.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
            if (existente is not null)
                return Dto(existente);

            var usuario = await db.Users.Where(x => x.Id == usuarioId)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.AvatarSolicitacaoAtualId, id), ct);
            if (usuario == 0)
                return null;

            await CancelarPendentesAsync(usuarioId, ct);
            if (await db.SolicitacoesAvatar.CountAsync(x => x.Status == "pending", ct) >= 100)
                throw new LimiteProcessamentosAvatarException();

            db.SolicitacoesAvatar.Add(solicitacao);
            // Bus Outbox grava a mensagem na mesma transação da solicitação.
            // Envio à fila também funciona antes do primeiro consumer iniciar.
            var endpoint = await mensagens.GetSendEndpoint(new Uri($"queue:{ProcessarAvatarMessage.Fila}"));
            await endpoint.Send(new ProcessarAvatarMessage(id), ct);
            await db.SaveChangesAsync(ct);
            await transacao.CommitAsync(ct);
            return Dto(solicitacao);
        });
    }

    public async Task<bool> RemoverAsync(Guid usuarioId, CancellationToken ct)
    {
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transacao = await db.Database.BeginTransactionAsync(ct);
            var alterados = await db.Users.Where(x => x.Id == usuarioId).ExecuteUpdateAsync(s => s
                .SetProperty(x => x.AvatarUrl, (string?)null)
                .SetProperty(x => x.AvatarSolicitacaoAtualId, (Guid?)null), ct);
            if (alterados == 0)
                return false;
            await CancelarPendentesAsync(usuarioId, ct);
            await db.Avatares.Where(x => x.UsuarioId == usuarioId).ExecuteDeleteAsync(ct);
            await transacao.CommitAsync(ct);
            return true;
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

    public async Task<ProcessamentoAvatarDto?> ObterStatusAsync(Guid usuarioId, Guid solicitacaoId, CancellationToken ct)
    {
        return await db.SolicitacoesAvatar.AsNoTracking()
            .Where(x => x.Id == solicitacaoId && x.UsuarioId == usuarioId)
            .Select(x => new ProcessamentoAvatarDto(x.Id, x.Status, x.Erro)).SingleOrDefaultAsync(ct);
    }

    public async Task<byte[]?> IniciarProcessamentoAsync(Guid solicitacaoId, CancellationToken ct)
    {
        // Contador persistido também limita redeliveries após queda do processo nativo.
        var expirado = DateTime.UtcNow.AddHours(-24);
        await db.SolicitacoesAvatar.Where(x => x.Id == solicitacaoId && x.Status == "pending"
            && (x.Tentativas >= 3 || x.CriadoEmUtc < expirado)).ExecuteUpdateAsync(s => s
                .SetProperty(x => x.Status, "failed")
                .SetProperty(x => x.Erro, "Não foi possível processar a imagem dentro dos limites.")
                .SetProperty(x => x.Arquivo, (byte[]?)null), ct);

        var alterados = await db.SolicitacoesAvatar.Where(x => x.Id == solicitacaoId && x.Status == "pending"
            && db.Users.Any(u => u.Id == x.UsuarioId && u.AvatarSolicitacaoAtualId == x.Id))
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Tentativas, x => x.Tentativas + 1), ct);
        if (alterados == 0)
            return null;
        return await db.SolicitacoesAvatar.AsNoTracking().Where(x => x.Id == solicitacaoId && x.Status == "pending")
            .Select(x => x.Arquivo).SingleOrDefaultAsync(ct);
    }

    public async Task FinalizarAsync(Guid solicitacaoId, byte[]? imagem, string? erro, CancellationToken ct)
    {
        await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            var solicitacao = await db.SolicitacoesAvatar.AsNoTracking().SingleOrDefaultAsync(x => x.Id == solicitacaoId, ct);
            if (solicitacao is null || solicitacao.Status != "pending")
                return;
            await using var transacao = await db.Database.BeginTransactionAsync(ct);
            // O bloqueio do titular evita ressuscitar avatar removido ou sobrescrever upload mais novo.
            var titular = await db.Users.Where(x => x.Id == solicitacao.UsuarioId && x.AvatarSolicitacaoAtualId == solicitacaoId)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.AvatarSolicitacaoAtualId, solicitacaoId), ct);
            if (titular == 0)
                return;
            var alterados = await db.SolicitacoesAvatar.Where(x => x.Id == solicitacaoId && x.Status == "pending")
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, imagem == null ? "rejected" : "completed")
                    .SetProperty(x => x.Erro, erro).SetProperty(x => x.Arquivo, (byte[]?)null), ct);
            if (alterados == 0)
                return;

            if (imagem is not null)
            {
                await db.Avatares.Where(x => x.UsuarioId == solicitacao.UsuarioId).ExecuteDeleteAsync(ct);
                await db.Database.ExecuteSqlInterpolatedAsync(
                    $"INSERT INTO Avatares (UsuarioId, Conteudo) VALUES ({solicitacao.UsuarioId}, {imagem})", ct);
                await db.Users.Where(x => x.Id == solicitacao.UsuarioId).ExecuteUpdateAsync(s => s
                    .SetProperty(x => x.AvatarUrl, x => "/api/users/" + x.UserName + "/avatar"), ct);
            }
            await transacao.CommitAsync(ct);
        });
    }

    public async Task MarcarFalhaAsync(Guid solicitacaoId, CancellationToken ct)
    {
        await db.SolicitacoesAvatar.Where(x => x.Id == solicitacaoId && x.Status == "pending")
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, "failed")
                .SetProperty(x => x.Erro, "Não foi possível processar a imagem.")
                .SetProperty(x => x.Arquivo, (byte[]?)null), ct);
    }

    private Task<int> CancelarPendentesAsync(Guid usuarioId, CancellationToken ct) =>
        db.SolicitacoesAvatar.Where(x => x.UsuarioId == usuarioId && x.Status == "pending")
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, "cancelled")
                .SetProperty(x => x.Arquivo, (byte[]?)null), ct);

    private static ProcessamentoAvatarDto Dto(SolicitacaoAvatar x) => new(x.Id, x.Status, x.Erro);
}
