namespace TBE.BookingService.Application.Ttl;

/// <summary>
/// Configuration for <see cref="TtlMonitorHostedService"/>. Bound from the <c>TtlMonitor</c>
/// configuration section.
/// </summary>
public sealed class TtlMonitorOptions
{
    /// <summary>
    /// Interval between poll iterations. Default 5 minutes per D-04 / RESEARCH §"Pattern 6".
    /// </summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromMinutes(5);
}
