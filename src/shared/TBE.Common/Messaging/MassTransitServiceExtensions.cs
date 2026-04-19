using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace TBE.Common.Messaging;

/// <summary>
/// Shared MassTransit registration helper.
/// Services call AddTbeMassTransitWithRabbitMq() and pass their own consumers and outbox config.
/// </summary>
public static class MassTransitServiceExtensions
{
    /// <summary>
    /// Registers MassTransit with RabbitMQ transport.
    /// Callers provide consumer registration and optional outbox configuration.
    /// </summary>
    public static IServiceCollection AddTbeMassTransitWithRabbitMq(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<IBusRegistrationConfigurator>? configureConsumers = null,
        Action<IBusRegistrationConfigurator>? configureOutbox = null,
        Action<IBusRegistrationContext, IRabbitMqBusFactoryConfigurator>? configureBus = null)
    {
        services.AddMassTransit(x =>
        {
            // Register service-specific consumers
            configureConsumers?.Invoke(x);

            // Register outbox (called before UsingRabbitMq per MassTransit docs)
            configureOutbox?.Invoke(x);

            x.UsingRabbitMq((context, cfg) =>
            {
                cfg.Host(
                    configuration["RabbitMQ:Host"] ?? "rabbitmq",
                    "/",
                    h =>
                    {
                        h.Username(configuration["RabbitMQ:Username"] ?? "guest");
                        h.Password(configuration["RabbitMQ:Password"] ?? "guest");
                    });

                // Plan 06-01 — allow callers to register explicit receive
                // endpoints (used by BackofficeService to bind ErrorQueueConsumer
                // to the 10 known _error queues) BEFORE ConfigureEndpoints runs.
                configureBus?.Invoke(context, cfg);

                cfg.ConfigureEndpoints(context);
            });
        });

        return services;
    }
}
