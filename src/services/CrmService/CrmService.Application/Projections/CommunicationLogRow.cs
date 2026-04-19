namespace TBE.CrmService.Application.Projections;

/// <summary>
/// Plan 06-04 Task 1 / CRM-04 — free-form ops note persisted against a
/// Customer or Agency. Markdown body per D-62 (sanitised client-side —
/// no HTML stored). Written by <c>CommunicationLogController</c> and by
/// the <c>CustomerCommunicationLoggedConsumer</c> (which handles
/// cross-service log publishes).
/// </summary>
public sealed class CommunicationLogRow
{
    public Guid LogId { get; set; }

    /// <summary>'Customer' | 'Agency'.</summary>
    public string EntityType { get; set; } = string.Empty;

    public Guid EntityId { get; set; }

    public string CreatedBy { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    /// <summary>Markdown source (plain text with markdown syntax); max 10000 chars.</summary>
    public string Body { get; set; } = string.Empty;
}
