using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;

namespace TBE.FlightConnectorService.Application.Amadeus;

public class AmadeusAuthHandler(
    IHttpClientFactory httpClientFactory,
    IOptionsMonitor<AmadeusOptions> opts,
    ILogger<AmadeusAuthHandler> logger) : DelegatingHandler
{
    private string? _cachedToken;
    private DateTimeOffset _tokenExpiry = DateTimeOffset.MinValue;
    private readonly SemaphoreSlim _lock = new(1, 1);

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (DateTimeOffset.UtcNow >= _tokenExpiry.AddSeconds(-30))
            await RefreshTokenAsync(cancellationToken);

        request.Headers.Authorization = new("Bearer", _cachedToken);
        return await base.SendAsync(request, cancellationToken);
    }

    private async Task RefreshTokenAsync(CancellationToken ct)
    {
        await _lock.WaitAsync(ct);
        try
        {
            if (DateTimeOffset.UtcNow < _tokenExpiry.AddSeconds(-30)) return; // double-check
            var o = opts.CurrentValue;
            var client = httpClientFactory.CreateClient("amadeus-auth");
            var body = new FormUrlEncodedContent([
                new("grant_type", "client_credentials"),
                new("client_id", o.ApiKey),
                new("client_secret", o.ApiSecret),
            ]);
            var resp = await client.PostAsync(o.TokenUrl, body, ct);
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadFromJsonAsync<AmadeusTokenResponse>(ct);
            _cachedToken = json!.AccessToken;
            _tokenExpiry = DateTimeOffset.UtcNow.AddSeconds(json.ExpiresIn);
            logger.LogInformation("Amadeus token refreshed, expires at {Expiry}", _tokenExpiry);
            // SECURITY: never log _cachedToken value
        }
        finally { _lock.Release(); }
    }
}
