using System.Collections.Generic;
using Xunit;

namespace TBE.Tests.Shared.Fixtures;

/// <summary>
/// Canned fare-rule payload fixtures keyed by GDS provider code.
/// Real captured fixtures are a Wave 0 follow-up delivered by plan 03-03 fare rule parser tests;
/// these placeholders exist so consumer test projects can compile against the shared surface today.
/// </summary>
public sealed class GdsSandboxFixture
{
    public IReadOnlyDictionary<string, string> FareRulePayloads { get; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["amadeus"] = "{\"fareBasis\":\"PLACEHOLDER_AMADEUS\",\"rules\":[]}",
            ["sabre"] = "<FareRules><Rule code=\"PLACEHOLDER_SABRE\"/></FareRules>",
            ["galileo"] = "PLACEHOLDER_GALILEO\nRULE 1 NON-REFUNDABLE",
        };
}

[CollectionDefinition(nameof(GdsSandboxFixture))]
public sealed class GdsSandboxFixtureCollection : ICollectionFixture<GdsSandboxFixture>
{
}
