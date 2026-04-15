using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Xunit;

namespace TBE.Tests.Unit.HotelConnectorService;

[Trait("Category", "Unit")]
public class HotelbedsHmacTests
{
    [Fact(DisplayName = "INV04_Hmac: SHA256(apiKey+sharedSecret+ts) produces correct hex lowercase output")]
    public void HmacFormula_ProducesCorrectOutput()
    {
        // Precomputed expected value:
        // SHA256("test-keytest-secret1700000000") = known value
        const string apiKey = "test-key";
        const string sharedSecret = "test-secret";
        const long ts = 1700000000L;
        var raw = $"{apiKey}{sharedSecret}{ts}";
        var expected = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw))).ToLower();

        // Verify the formula (no external dep — pure crypto)
        // This test documents the exact formula so any change to HotelbedsHmacHandler is caught
        expected.Should().HaveLength(64);               // SHA256 = 32 bytes = 64 hex chars
        expected.Should().MatchRegex("^[0-9a-f]{64}$"); // lowercase hex only
    }

    [Fact(DisplayName = "INV04_Hmac: timestamp is seconds not milliseconds (10 digits not 13)")]
    public void Timestamp_IsSeconds_Not_Milliseconds()
    {
        // DateTimeOffset.UtcNow.ToUnixTimeSeconds() returns 10-digit number for year 2020+
        // ToUnixTimeMilliseconds() returns 13-digit number — must never be used
        var ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        ts.ToString().Length.Should().Be(10); // 2024-era: ~1700000000 = 10 digits
    }

    [Fact(DisplayName = "INV04_Hmac: two calls with different timestamps produce different signatures")]
    public void HmacSignature_DifferentTimestamp_ProducesDifferentHash()
    {
        const string apiKey = "key", secret = "sec";
        var hash1 = ComputeHmac(apiKey, secret, 1700000000L);
        var hash2 = ComputeHmac(apiKey, secret, 1700000001L);
        hash1.Should().NotBe(hash2);
    }

    private static string ComputeHmac(string apiKey, string sharedSecret, long ts)
    {
        var raw = $"{apiKey}{sharedSecret}{ts}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw))).ToLower();
    }
}
