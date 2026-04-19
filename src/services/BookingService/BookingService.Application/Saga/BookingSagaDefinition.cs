using MassTransit;

namespace TBE.BookingService.Application.Saga;

/// <summary>
/// Endpoint-level configuration for the booking saga. Wires D-02 retry policy:
/// exponential backoff 3 attempts (2s → 4s → 8s). After exhaustion, the faulted
/// message surfaces back into the saga's own failure-event handlers which emit
/// <c>SagaDeadLetterRequested</c> where appropriate (capture path).
///
/// Plan 06-01 Task 5 — <see cref="BookingEventsObserver"/> is connected to the
/// state machine directly in <see cref="BookingSaga"/>'s constructor via
/// <c>ConnectStateObserver</c>, so every <c>TransitionTo</c> writes a
/// <c>dbo.BookingEvents</c> row via <see cref="IBookingEventsWriter"/> (BO-05 /
/// D-50) without additional endpoint-level plumbing here.
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
