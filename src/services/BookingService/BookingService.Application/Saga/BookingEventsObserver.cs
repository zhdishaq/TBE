using MassTransit;
using Microsoft.Extensions.DependencyInjection;

namespace TBE.BookingService.Application.Saga;

/// <summary>
/// Plan 06-01 Task 5 (BO-05 / D-50) — saga state observer that writes
/// one <c>dbo.BookingEvents</c> row per state transition.
///
/// MassTransit's <see cref="IStateObserver{TInstance}"/> fires
/// <see cref="StateChanged"/> on every TransitionTo activity the saga
/// executes, regardless of which triggering message routed us there.
/// This keeps the state-machine definition free of audit plumbing: a
/// new forward state added later in Plan 06-02+ automatically gets a
/// BookingEvents row without a second edit.
///
/// Fire-and-log: failure in the audit append does NOT fail the saga
/// transition that already moved state. The writer swallows persistence
/// exceptions (see BookingEventsWriter). Observer itself still returns
/// a completed Task so an audit DB outage does not block the saga bus.
/// </summary>
public sealed class BookingEventsObserver : IStateObserver<BookingSagaState>
{
    public const string SystemActor = "system:BookingSaga";

    public Task StateChanged(BehaviorContext<BookingSagaState> context, State currentState, State? previousState)
    {
        return WriteAsync(context, currentState, previousState);
    }

    private static async Task WriteAsync(BehaviorContext<BookingSagaState> context, State currentState, State? previousState)
    {
        if (currentState is null) return;

        IBookingEventsWriter? writer;
        try
        {
            if (!context.TryGetPayload<IServiceProvider>(out var sp) || sp is null)
                return;
            writer = sp.GetService<IBookingEventsWriter>();
            if (writer is null) return;
        }
        catch
        {
            return;
        }

        var state = context.Saga;
        var eventType = DeriveEventType(context, currentState);

        var snapshot = new
        {
            BookingId = state.CorrelationId,
            Channel = state.Channel.ToString(),
            Status = currentState.Name,
            PreviousState = previousState?.Name,
            PricingBreakdown = new
            {
                state.BaseFareAmount,
                state.SurchargeAmount,
                state.TaxAmount,
                GrossAmount = state.TotalAmount,
                state.AgencyNetFare,
                MarkupAmount = state.AgencyMarkupAmount,
                CommissionAmount = state.AgencyCommissionAmount,
            },
            SupplierResponse = new
            {
                Pnr = state.GdsPnr,
                TicketNumber = state.TicketNumber,
                GdsRecordLocator = state.GdsPnr,
            },
        };

        var correlationId = context is ConsumeContext cc ? (cc.CorrelationId ?? state.CorrelationId) : state.CorrelationId;

        await writer.WriteAsync(
            bookingId: state.CorrelationId,
            eventType: eventType,
            actor: SystemActor,
            correlationId: correlationId,
            snapshotPayload: snapshot,
            ct: context.CancellationToken);
    }

    private static string DeriveEventType(BehaviorContext<BookingSagaState> context, State currentState)
    {
        // Prefer the triggering message type name — that's what ops tooling
        // reads. Fall back to the destination state name.
        if (context is ConsumeContext cc)
        {
            var msgType = cc.SupportedMessageTypes.FirstOrDefault();
            if (!string.IsNullOrEmpty(msgType))
            {
                var lastDot = msgType.LastIndexOf('.');
                if (lastDot >= 0 && lastDot < msgType.Length - 1)
                    return msgType[(lastDot + 1)..];
                return msgType;
            }
        }
        return currentState.Name;
    }
}
