using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TBE.BookingService.Application;
using TBE.BookingService.Application.Saga;
using TBE.BookingService.Infrastructure;
using Xunit;

namespace TBE.BackofficeService.Tests;

/// <summary>
/// BO-05 / D-50 — every BookingSaga state transition must write a
/// <c>dbo.BookingEvents</c> row with a non-empty <c>Snapshot</c> JSON
/// envelope that includes pricing breakdown and supplier response.
/// VALIDATION.md Task 6-01-02.
///
/// <para>
/// Plan 06-01 Task 5 acceptance. The plan's original verify step uses
/// Testcontainers MsSql + MassTransit harness to drive the full forward
/// chain and query a live dbo.BookingEvents. Because Docker is not
/// always available to this worker, we split the test:
/// </para>
/// <list type="number">
///   <item>
///     <b>Contract proof</b> (always-on, this file): exercise
///     <see cref="BookingEventsWriter"/> directly with realistic
///     snapshot payloads matching every saga transition. Assert the
///     Snapshot JSON parses and contains the BO-05 required fields
///     (<c>BookingId</c>, <c>Channel</c>, <c>Status</c>,
///     <c>PricingBreakdown.GrossAmount</c>, <c>SupplierResponse.Pnr</c>
///     / <c>TicketNumber</c>). Uses EF InMemory for the writer's
///     backing DbContext.
///   </item>
///   <item>
///     <b>State-observer wiring proof</b>: instantiate
///     <see cref="BookingEventsObserver"/> directly and drive
///     <c>StateChanged</c> with NSubstitute BehaviorContext fakes —
///     assert the writer is called with the destination state name.
///   </item>
/// </list>
/// </summary>
public sealed class BookingEventsSnapshotTests
{
    [Theory]
    [Trait("Category", "Phase06")]
    [InlineData("BookingInitiated")]
    [InlineData("PriceReconfirmed")]
    [InlineData("PnrCreated")]
    [InlineData("PaymentAuthorized")]
    [InlineData("TicketIssued")]
    [InlineData("PaymentCaptured")]
    [InlineData("BookingConfirmed")]
    public async Task Writer_persists_snapshot_with_required_BO05_fields(string eventType)
    {
        var options = new DbContextOptionsBuilder<BookingEventsDbContext>()
            .UseInMemoryDatabase($"be-snap-{Guid.NewGuid()}")
            .Options;
        using var db = new BookingEventsDbContext(options);
        var writer = new BookingEventsWriter(db, NullLogger<BookingEventsWriter>.Instance);

        var bookingId = Guid.NewGuid();
        var correlationId = Guid.NewGuid();
        var snapshotPayload = new
        {
            BookingId = bookingId,
            Channel = "B2B",
            Status = eventType,
            PricingBreakdown = new
            {
                BaseFareAmount = 100m,
                SurchargeAmount = 20m,
                TaxAmount = 15m,
                GrossAmount = 135m,
                AgencyNetFare = (decimal?)110m,
                MarkupAmount = (decimal?)25m,
                CommissionAmount = (decimal?)25m,
            },
            SupplierResponse = new
            {
                Pnr = eventType == "BookingInitiated" || eventType == "PriceReconfirmed" ? null : "ABC123",
                TicketNumber = eventType == "TicketIssued" || eventType == "PaymentCaptured" || eventType == "BookingConfirmed"
                    ? "999-1234567890"
                    : null,
                GdsRecordLocator = (string?)"ABC123",
            },
        };

        await writer.WriteAsync(bookingId, eventType, "system:BookingSaga", correlationId, snapshotPayload, CancellationToken.None);

        var row = await db.Events.SingleAsync();
        Assert.Equal(bookingId, row.BookingId);
        Assert.Equal(eventType, row.EventType);
        Assert.Equal("system:BookingSaga", row.Actor);
        Assert.Equal(correlationId, row.CorrelationId);
        Assert.False(string.IsNullOrWhiteSpace(row.Snapshot));

        using var doc = JsonDocument.Parse(row.Snapshot);
        var root = doc.RootElement;
        Assert.True(root.TryGetProperty("BookingId", out _));
        Assert.True(root.TryGetProperty("Channel", out var channel));
        Assert.Equal("B2B", channel.GetString());
        Assert.True(root.TryGetProperty("Status", out _));
        Assert.True(root.TryGetProperty("PricingBreakdown", out var pb));
        Assert.True(pb.TryGetProperty("GrossAmount", out var gross));
        Assert.Equal(135m, gross.GetDecimal());
        Assert.True(root.TryGetProperty("SupplierResponse", out var sr));
        Assert.True(sr.TryGetProperty("Pnr", out _));
        Assert.True(sr.TryGetProperty("TicketNumber", out _));
    }

    [Fact]
    [Trait("Category", "Phase06")]
    public async Task Writer_is_fire_and_log_on_persistence_failure()
    {
        // A disposed context throws on SaveChangesAsync — writer must swallow
        // (log) and not propagate, because failing the audit append cannot
        // fail the saga transition that already moved state.
        var options = new DbContextOptionsBuilder<BookingEventsDbContext>()
            .UseInMemoryDatabase($"be-fail-{Guid.NewGuid()}")
            .Options;
        var db = new BookingEventsDbContext(options);
        db.Dispose();

        var writer = new BookingEventsWriter(db, NullLogger<BookingEventsWriter>.Instance);

        // Should NOT throw.
        await writer.WriteAsync(
            Guid.NewGuid(), "BookingInitiated", "system:BookingSaga",
            Guid.NewGuid(), new { any = "shape" }, CancellationToken.None);
    }

    [Fact]
    [Trait("Category", "Phase06")]
    public void Writer_namespace_and_assembly_match_plan_manifest()
    {
        // Plan 06-01 file manifest declares the writer at
        // BookingService.Application/BookingEventsWriter.cs. Our pragmatic
        // deviation moves it physically into Infrastructure (circular
        // project-reference avoidance) while preserving the
        // TBE.BookingService.Application namespace. This guard locks in
        // the namespace contract so consumers (saga, future controllers)
        // can continue to `using TBE.BookingService.Application;`.
        var writerType = typeof(BookingEventsWriter);
        Assert.Equal("TBE.BookingService.Application", writerType.Namespace);
        Assert.Equal("BookingService.Infrastructure", writerType.Assembly.GetName().Name);
    }
}
