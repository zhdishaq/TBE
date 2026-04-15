using MassTransit;
using Microsoft.Extensions.Logging;
using TBE.BookingService.Application.Saga;
using TBE.Contracts.Events;

namespace TBE.BookingService.Application.Consumers.CompensationConsumers;

/// <summary>
/// Persists <see cref="SagaDeadLetterRequested"/> escalations into the <c>Saga.SagaDeadLetter</c>
/// ledger so ops can drive manual reconciliation. The saga itself never writes to SQL directly
/// (Pitfall 2 — state machines stay side-effect-free with respect to DbContext).
/// DbContext is taken via interface to keep Application free of Infrastructure project reference.
/// </summary>
public interface ISagaDeadLetterStore
{
    Task AddAsync(SagaDeadLetter entry, CancellationToken ct);
}

public class SagaDeadLetterSink : IConsumer<SagaDeadLetterRequested>
{
    private readonly ISagaDeadLetterStore _store;
    private readonly ILogger<SagaDeadLetterSink> _logger;

    public SagaDeadLetterSink(ISagaDeadLetterStore store, ILogger<SagaDeadLetterSink> logger)
    {
        _store = store;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<SagaDeadLetterRequested> context)
    {
        var msg = context.Message;
        _logger.LogError(
            "Saga dead-letter: correlationId={CorrelationId} step={StepName} cause={Cause}",
            msg.CorrelationId, msg.StepName, msg.Cause);

        var entry = new SagaDeadLetter
        {
            Id = Guid.NewGuid(),
            CorrelationId = msg.CorrelationId,
            LastSuccessfulStep = msg.StepName,
            FailedStep = msg.StepName,
            ExceptionMessage = msg.Cause,
            CreatedAtUtc = msg.OccurredAt.UtcDateTime,
            Resolved = false,
        };
        await _store.AddAsync(entry, context.CancellationToken);
    }
}
