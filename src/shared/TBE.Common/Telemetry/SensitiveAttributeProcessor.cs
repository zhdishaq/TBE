using System.Diagnostics;
using System.Text.RegularExpressions;
using OpenTelemetry;

namespace TBE.Common.Telemetry;

/// <summary>
/// OpenTelemetry <see cref="BaseProcessor{T}"/> that redacts PCI / PII span attributes before
/// they reach any exporter. Must be registered via <c>AddProcessor</c> BEFORE any exporter in the
/// pipeline — <see cref="TelemetryServiceExtensions.AddTbeOpenTelemetry"/> wires the correct order.
/// Replacement value is the constant <see cref="Redacted"/>.
/// Threat: T-03-05 / COMP-06.
/// </summary>
public sealed class SensitiveAttributeProcessor : BaseProcessor<Activity>
{
    public const string Redacted = "[REDACTED]";

    /// <summary>Exact-match forbidden keys (case-insensitive).</summary>
    private static readonly HashSet<string> ForbiddenKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "card.number", "card.cvv", "card.expiry",
        "cvv", "pan",
        "stripe.raw_payment_method", "stripe.raw_body",
        "passport.number", "passport.raw",
        "document.number",
    };

    /// <summary>Prefix patterns flagged as sensitive regardless of suffix.</summary>
    private static readonly Regex ForbiddenKeyPattern = new(
        @"^(card\.|stripe\.raw_|passport\.)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public override void OnEnd(Activity activity)
    {
        if (activity is null) return;

        foreach (var tag in activity.TagObjects)
        {
            if (IsSensitive(tag.Key))
            {
                activity.SetTag(tag.Key, Redacted);
            }
        }
    }

    internal static bool IsSensitive(string key) =>
        !string.IsNullOrEmpty(key) &&
        (ForbiddenKeys.Contains(key) || ForbiddenKeyPattern.IsMatch(key));
}
