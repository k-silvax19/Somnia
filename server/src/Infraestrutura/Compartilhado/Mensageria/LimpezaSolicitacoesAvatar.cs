using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Somnia.Infraestrutura.Compartilhado.Orm;

namespace Somnia.Infraestrutura.Compartilhado.Mensageria;

public sealed class LimpezaSolicitacoesAvatar(IServiceScopeFactory scopes, ILogger<LimpezaSolicitacoesAvatar> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(10));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<SomniaDbContext>();
                var limite = DateTime.UtcNow.AddHours(-24);
                await db.SolicitacoesAvatar.Where(x => x.Status == "pending" && x.CriadoEmUtc < limite)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, "failed")
                        .SetProperty(x => x.Erro, "Prazo de processamento expirado.")
                        .SetProperty(x => x.Arquivo, (byte[]?)null), stoppingToken);
                await db.SolicitacoesAvatar.Where(x => x.Status != "pending" && x.CriadoEmUtc < limite)
                    .ExecuteDeleteAsync(stoppingToken);
            }
            catch (Exception) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogWarning("Não foi possível limpar solicitações antigas de avatar; haverá nova tentativa.");
            }
            if (!await timer.WaitForNextTickAsync(stoppingToken))
                break;
        }
    }
}
