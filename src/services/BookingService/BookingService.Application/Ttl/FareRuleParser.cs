using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace TBE.BookingService.Application.Ttl;

/// <summary>
/// Chain-of-responsibility fare-rule parser. Dispatches to the keyed
/// <see cref="IFareRuleAdapter"/> registered under the incoming GDS code.
/// Applies the Pitfall-5 past-deadline guard (already enforced inside each adapter but
/// re-asserted here defensively).
/// </summary>
public sealed class FareRuleParser : IFareRuleParser
{
    private readonly IServiceProvider _services;
    private readonly ILogger<FareRuleParser> _log;

    public FareRuleParser(IServiceProvider services, ILogger<FareRuleParser> log)
    {
        _services = services;
        _log = log;
    }

    public bool TryParse(string gdsCode, string rawPayload, out DateTime deadlineUtc)
    {
        deadlineUtc = default;
        if (string.IsNullOrWhiteSpace(gdsCode) || string.IsNullOrEmpty(rawPayload))
        {
            return false;
        }

        var key = gdsCode.Trim().ToLowerInvariant();
        var adapter = _services.GetKeyedService<IFareRuleAdapter>(key);
        if (adapter is null)
        {
            _log.LogWarning("No IFareRuleAdapter registered for gdsCode '{Gds}' — parse will fail", key);
            return false;
        }

        if (!adapter.TryParse(rawPayload, out var candidate))
        {
            return false;
        }

        // Defensive guard (Pitfall 5): never hand the saga a deadline in the past.
        if (candidate <= DateTime.UtcNow)
        {
            return false;
        }

        deadlineUtc = candidate;
        return true;
    }
}
