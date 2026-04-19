using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TBE.BackofficeService.Application.Entities;

/// <summary>
/// Persistent record of a MassTransit <c>_error</c>-queue envelope captured
/// by <see cref="Consumers.ErrorQueueConsumer"/>. Mirrors the D-58 schema
/// from 06-CONTEXT.md — append-only view maintained by the backoffice
/// portal BO-09/BO-10 surfaces.
/// </summary>
[Table("DeadLetterQueue", Schema = "backoffice")]
public sealed class DeadLetterQueueRow
{
    [Key]
    public Guid Id { get; set; }

    /// <summary>
    /// MessageId from the originating envelope (MassTransit
    /// <c>ConsumeContext.MessageId</c>). Preserved across requeue so
    /// downstream idempotency keys continue to work.
    /// </summary>
    public Guid MessageId { get; set; }

    /// <summary>Optional MassTransit CorrelationId (Plan 03-01 saga pattern).</summary>
    public Guid? CorrelationId { get; set; }

    [MaxLength(256)]
    public string MessageType { get; set; } = string.Empty;

    [MaxLength(256)]
    public string OriginalQueue { get; set; } = string.Empty;

    /// <summary>Full JSON envelope (payload + headers) as received on <c>_error</c>.</summary>
    public string Payload { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string FailureReason { get; set; } = string.Empty;

    public DateTime FirstFailedAt { get; set; }

    public DateTime? LastRequeuedAt { get; set; }

    public int RequeueCount { get; set; }

    public DateTime? ResolvedAt { get; set; }

    [MaxLength(128)]
    public string? ResolvedBy { get; set; }

    [MaxLength(500)]
    public string? ResolutionReason { get; set; }
}
