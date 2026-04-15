using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;

namespace TBE.HotelConnectorService.Application.Hotelbeds;

public class HotelbedsHmacHandler(IOptionsMonitor<HotelbedsOptions> opts) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var o = opts.CurrentValue;
        // PITFALL 5: Docker containers can have clock skew after host sleep/resume.
        // Use ToUnixTimeSeconds() — Hotelbeds rejects signatures > ~5 min from server time.
        // NEVER use DateTime.Now (local timezone), ticks, or milliseconds.
        var ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        // HMAC formula: SHA256(apiKey + sharedSecret + unixTimestampSeconds), hex lowercase
        // SECURITY: SharedSecret must NOT be logged anywhere in this class
        var raw = $"{o.ApiKey}{o.SharedSecret}{ts}";
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw))).ToLower();

        request.Headers.Remove("Api-key");
        request.Headers.Remove("X-Signature");
        request.Headers.Add("Api-key", o.ApiKey);
        request.Headers.Add("X-Signature", hash);
        // Accept-Encoding: gzip required by Hotelbeds API
        if (!request.Headers.Contains("Accept-Encoding"))
            request.Headers.Add("Accept-Encoding", "gzip");

        return base.SendAsync(request, cancellationToken);
    }
}
