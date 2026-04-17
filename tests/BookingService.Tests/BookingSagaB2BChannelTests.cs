using FluentAssertions;
using TBE.BookingService.Application.Saga;
using TBE.Contracts.Enums;
using TBE.Contracts.Events;
using Xunit;

namespace TBE.BookingService.Tests;

/// <summary>
/// Plan 05-02 Task 1 greens — state-shape + contract surface for the typed
/// Channel column on <see cref="BookingSagaState"/> and the supporting
/// <see cref="BookingInitiated"/> / <see cref="AgentBookingDetailsCaptured"/>
/// events. Keeps the Wave-0 pre-staged tests but replaces the Assert.Fail
/// placeholders with real structural assertions so the GREEN commit closes
/// the Task 1 gate.
/// </summary>
public class BookingSagaB2BChannelTests
{
    /// <summary>Default value must be 0 (Channel.B2C) so existing rows that predate the migration keep direct-customer semantics.</summary>
    [Fact]
    public void BookingSagaState_Channel_defaults_to_B2C()
    {
        var state = new BookingSagaState();
        state.Channel.Should().Be(Channel.B2C);
    }

    /// <summary>
    /// Plan 05-02 Task 2 — the B2B branch relies on the string-keyed
    /// <see cref="BookingInitiated.Channel"/> + <see cref="BookingInitiated.WalletId"/>
    /// (Phase-3 compat) plus the follow-up <see cref="AgentBookingDetailsCaptured"/>
    /// event carrying AgencyId + agency pricing snapshot.
    /// </summary>
    [Fact]
    public void BookingInitiated_carries_Channel_and_WalletId_and_AgentBookingDetailsCaptured_carries_AgencyId()
    {
        var initiated = new BookingInitiated(
            BookingId: Guid.NewGuid(),
            ProductType: "flight",
            Channel: "b2b",
            UserId: "agent-1",
            BookingReference: "TBE-260520-X",
            TotalAmount: 100m,
            Currency: "GBP",
            PaymentMethod: "wallet",
            WalletId: Guid.NewGuid(),
            InitiatedAt: DateTimeOffset.UtcNow);
        initiated.Channel.Should().Be("b2b");
        initiated.WalletId.Should().NotBeNull();

        var captured = new AgentBookingDetailsCaptured(
            BookingId: initiated.BookingId,
            AgencyId: Guid.NewGuid(),
            AgencyNetFare: 80m,
            AgencyMarkupAmount: 20m,
            AgencyGrossAmount: 100m,
            AgencyCommissionAmount: 20m,
            AgencyMarkupOverride: null,
            CustomerName: "Jane",
            CustomerEmail: "jane@example.com",
            CustomerPhone: "+44",
            OfferId: "o-1",
            At: DateTimeOffset.UtcNow);
        captured.AgencyId.Should().NotBe(Guid.Empty);
    }
}
