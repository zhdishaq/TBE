using TBE.Contracts.Enums;
using Xunit;

namespace TBE.BookingService.Tests;

/// <summary>
/// Red placeholders for Plan 05-02 Task 1 (BookingSagaState Channel column
/// migration) and the BookingInitiated contract expansion. Uses the
/// shared Channel enum from src/shared/TBE.Contracts/Enums/Channel.cs
/// (Plan 05-00 Task 3) so this file compiles on Wave 0 even though the
/// saga-state column is still a string at that point — intentional: the
/// tests Assert.Fail so the runtime type mismatch never executes.
/// </summary>
public class BookingSagaB2BChannelTests
{
    /// <summary>Default value must be 0 (Channel.B2C) so existing rows that predate the migration keep direct-customer semantics.</summary>
    [Fact]
    [Trait("Category", "RedPlaceholder")]
    public void BookingSagaState_Channel_defaults_to_B2C()
    {
        // Reference the shared enum so TBE.Contracts.Enums is not stripped as unused.
        _ = Channel.B2C;
        Assert.Fail("MISSING — Plan 05-02 Task 1 (BookingSagaState.Channel migration with default 0).");
    }

    /// <summary>BookingInitiated contract carries Channel + AgencyId + WalletId so the saga can branch at PnrCreated.</summary>
    [Fact]
    [Trait("Category", "RedPlaceholder")]
    public void BookingInitiated_carries_Channel_and_AgencyId_and_WalletId()
    {
        _ = Channel.B2B;
        Assert.Fail("MISSING — Plan 05-02 Task 1 (expand BookingInitiated with Channel/AgencyId/WalletId).");
    }
}
