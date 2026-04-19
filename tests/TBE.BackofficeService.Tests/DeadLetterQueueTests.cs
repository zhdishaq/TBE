using Xunit;

namespace TBE.BackofficeService.Tests;

/// <summary>
/// BO-09 / BO-10 — MassTransit <c>_error</c> queue envelopes must be
/// captured by <c>ErrorQueueConsumer</c> into
/// <c>backoffice.DeadLetterQueue</c> with full payload + headers. Requeue
/// preserves MessageId + CorrelationId (Research anti-pattern 3).
/// Resolve requires a reason and sets <c>ResolvedBy</c> from the
/// <c>preferred_username</c> claim. VALIDATION.md Task 6-01-05.
/// </summary>
public sealed class DeadLetterQueueTests
{
    [Fact]
    [Trait("Category", "Phase06")]
    [Trait("Category", "RedPlaceholder")]
    public void Error_queue_consumer_persists_envelope_requeue_preserves_correlation_BO09_BO10()
    {
        Assert.Fail(
            "MISSING — Plan 06-01 Task 4 implements ErrorQueueConsumer + DlqController. " +
            "Publish failing message; wait for harness to route to _error; assert row " +
            "exists with Payload + FailureReason. Requeue preserves MessageId + " +
            "CorrelationId; RequeueCount++. Resolve sets ResolvedBy = preferred_username.");
    }
}
