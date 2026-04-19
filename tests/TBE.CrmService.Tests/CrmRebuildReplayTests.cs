using Xunit;

namespace TBE.CrmService.Tests;

/// <summary>
/// Plan 06-04 / D-51 — CRM projections must be rebuildable by replaying
/// the raw event stream. A replay of the same MessageId set against a
/// freshly-migrated CrmDb must arrive at the same projection state with
/// no duplicate rows, because MassTransit's EF InboxState dedup rejects
/// the second delivery with the same MessageId.
///
/// <para>
/// Status: RED placeholder. Requires a live MSSQL Testcontainer +
/// RabbitMQ Testcontainer + the CrmService.Infrastructure migration
/// applied to prove the InboxState row is written inside the same
/// transaction as the projection upsert. Tagged
/// <c>Category=RedPlaceholder</c>.
/// </para>
/// </summary>
public sealed class CrmRebuildReplayTests
{
    [Fact]
    [Trait("Category", "RedPlaceholder")]
    [Trait("Category", "Phase06")]
    public void Replay_of_same_MessageId_produces_identical_projection_rowcount()
    {
        Assert.Fail("MISSING — requires MSSQL + RabbitMQ Testcontainers + CrmDb migration (D-51 rebuild).");
    }

    [Fact]
    [Trait("Category", "RedPlaceholder")]
    [Trait("Category", "Phase06")]
    public void InboxState_row_written_in_same_transaction_as_projection_upsert()
    {
        Assert.Fail("MISSING — requires MSSQL Testcontainer + MassTransit EF inbox (D-51 dedup).");
    }

    [Fact]
    [Trait("Category", "RedPlaceholder")]
    [Trait("Category", "Phase06")]
    public void Fresh_db_replay_from_event_stream_rebuilds_full_Customer360()
    {
        Assert.Fail("MISSING — requires MSSQL + RabbitMQ Testcontainers (D-51 rebuild path).");
    }

    [Fact]
    [Trait("Category", "RedPlaceholder")]
    [Trait("Category", "Phase06")]
    public void Out_of_order_BookingCancelled_before_BookingConfirmed_still_reaches_correct_final_state()
    {
        Assert.Fail("MISSING — requires MSSQL Testcontainer + ordered replay harness (D-51 ordering).");
    }
}
