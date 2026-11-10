using MassTransit;
using Microsoft.Extensions.Logging;
using Somnia.Dominio.Modulos.Perfis;

namespace Somnia.Aplicacao.Modulos.Perfis.Mensageria;

public sealed class ProcessarAvatarConsumer(
    IRepositorioAvatar repositorio,
    IProcessadorAvatar processador,
    ILogger<ProcessarAvatarConsumer> logger) : IConsumer<ProcessarAvatarMessage>
{
    public async Task Consume(ConsumeContext<ProcessarAvatarMessage> context)
    {
        var id = context.Message.SolicitacaoId;
        var ct = context.CancellationToken;
        try
        {
            var arquivo = await repositorio.IniciarProcessamentoAsync(id, ct);
            if (arquivo is null)
                return; // Mensagem duplicada, cancelada, expirada ou substituída.

            byte[] imagem;
            try
            {
                imagem = processador.Normalizar(arquivo);
            }
            catch (ArgumentException ex)
            {
                await repositorio.FinalizarAsync(id, null, ex.Message, ct);
                return;
            }

            await repositorio.FinalizarAsync(id, imagem, null, ct);
            logger.LogInformation("Avatar da solicitação {SolicitacaoId} processado.", id);
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            if (context.GetRetryAttempt() >= 3)
                await repositorio.MarcarFalhaAsync(id, ct);
            throw; // MassTransit faz retry limitado e encaminha falhas para a fila _error.
        }
    }
}
