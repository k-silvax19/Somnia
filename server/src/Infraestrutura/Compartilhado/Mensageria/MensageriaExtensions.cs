using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Somnia.Aplicacao.Modulos.Perfis.Mensageria;
using Somnia.Dominio.Modulos.Perfis;
using Somnia.Infraestrutura.Compartilhado.Orm;
using Somnia.Infraestrutura.Modulos.Perfis;

namespace Somnia.Infraestrutura.Compartilhado.Mensageria;

public static class MensageriaExtensions
{
    public static IServiceCollection AddAvatarMessaging(this IServiceCollection services,
        IConfiguration configuration, bool consumidor = false)
    {
        var conexao = configuration.GetConnectionString("RabbitMq")
            ?? throw new InvalidOperationException("Connection string RabbitMq não configurada.");
        if (!Uri.TryCreate(conexao, UriKind.Absolute, out var uri) || uri.Scheme is not ("rabbitmq" or "rabbitmqs"))
            throw new InvalidOperationException("Connection string RabbitMq inválida.");

        services.AddMassTransit(config =>
        {
            if (consumidor)
                config.AddConsumer<ProcessarAvatarConsumer>();
            else
                config.AddEntityFrameworkOutbox<SomniaDbContext>(outbox =>
                {
                    outbox.UseSqlServer();
                    outbox.QueryDelay = TimeSpan.FromSeconds(1);
                    outbox.UseBusOutbox();
                });

            config.UsingRabbitMq((context, rabbit) =>
            {
                rabbit.Host(uri);
                if (consumidor)
                {
                    rabbit.ReceiveEndpoint(ProcessarAvatarMessage.Fila, endpoint =>
                    {
                        endpoint.PrefetchCount = 1;
                        endpoint.ConcurrentMessageLimit = 1;
                        endpoint.UseMessageRetry(retry => retry.Intervals(
                            TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(5)));
                        endpoint.ConfigureConsumer<ProcessarAvatarConsumer>(context);
                    });
                }
            });
        });
        services.Configure<MassTransitHostOptions>(options =>
        {
            options.WaitUntilStarted = consumidor;
            options.StartTimeout = TimeSpan.FromSeconds(30);
            options.StopTimeout = TimeSpan.FromSeconds(30);
        });
        if (consumidor)
        {
            services.AddSingleton<IProcessadorAvatar, ProcessadorAvatar>();
            services.AddHostedService<LimpezaSolicitacoesAvatar>();
        }
        return services;
    }
}
