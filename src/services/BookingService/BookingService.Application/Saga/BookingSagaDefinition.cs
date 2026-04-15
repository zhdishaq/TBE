using MassTransit;

namespace TBE.BookingService.Application.Saga;

/// <summary>
/// Endpoint-level configuration for the booking saga. Wires D-02 retry policy:
/// exponential backoff 3 attempts (2s → 4s → 8s). After exhaustion, the faulted
/// message surfaces back into the saga's own failure-event handlers which emit
/// <c>SagaDeadLetterRequested</c> where appropriate (capture path).
/// </summary>
public class BookingSagaDefinition : SagaDefinition<BookingSagaState>
{
    protected override void ConfigureSaga(
        IReceiveEndpointConfigurator endpointConfigurator,
        ISagaConfigurator<BookingSagaState> sagaConfigurator,
        IRegistrationContext context)
    {
        endpointConfigurator.UseMessageRetry(r => r.Exponential(
            retryLimit: 3,
            minInterval: TimeSpan.FromSeconds(2),
            maxInterval: TimeSpan.FromSeconds(8),
            intervalDelta: TimeSpan.FromSeconds(2)));
    }
}
