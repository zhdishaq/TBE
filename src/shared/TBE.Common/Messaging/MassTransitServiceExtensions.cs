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
        Action<IBusRegistrationConfigurator>? configureOutbox = null)
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

                cfg.ConfigureEndpoints(context);
            });
        });

        return services;
    }
}
