using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace TBE.Tests.Shared.Fixtures;

/// <summary>
/// Shared deterministic clock fixture backed by <see cref="FakeTimeProvider"/>.
/// Consumers inject <see cref="Clock"/> wherever a <c>TimeProvider</c> is required
/// so tests can assert behaviour at precise wall-clock offsets without sleeping.
/// </summary>
public sealed class ClockFixture
{
    /// <summary>
    /// Deterministic time provider. Start value is UTC now at fixture construction.
    /// Use <see cref="Advance(TimeSpan)"/> to move it forward.
    /// </summary>
    public FakeTimeProvider Clock { get; } = new(DateTimeOffset.UtcNow);

    /// <summary>
    /// Advance the clock by <paramref name="delta"/>. Fires any registered timers.
    /// </summary>
    public void Advance(TimeSpan delta) => Clock.Advance(delta);
}

[CollectionDefinition(nameof(ClockFixture))]
public sealed class ClockFixtureCollection : ICollectionFixture<ClockFixture>
{
}
