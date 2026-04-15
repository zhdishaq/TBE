namespace TBE.BookingService.Application.Saga;

/// <summary>
/// Unrecoverable-failure ledger row. Written by <c>SagaDeadLetterSink</c> when the saga
/// publishes a <c>SagaDeadLetterRequested</c> event (e.g. capture retries exhausted).
/// Reviewed by ops to drive manual reconciliation — the saga does NOT auto-void/refund
/// in this path (per D-03: capture failures must not blindly cancel the booking).
/// </summary>
public class SagaDeadLetter
{
    public Guid Id { get; set; }
    public Guid CorrelationId { get; set; }
    public string LastSuccessfulStep { get; set; } = string.Empty;
    public string FailedStep { get; set; } = string.Empty;
    public string ExceptionMessage { get; set; } = string.Empty;
    public string? ExceptionDetail { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public bool Resolved { get; set; }
}
