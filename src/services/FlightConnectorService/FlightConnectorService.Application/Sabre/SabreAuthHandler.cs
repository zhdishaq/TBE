using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;

namespace TBE.FlightConnectorService.Application.Sabre;

public class SabreAuthHandler(
    IHttpClientFactory httpClientFactory,
    IOptionsMonitor<SabreOptions> opts,
    ILogger<SabreAuthHandler> logger) : DelegatingHandler
{
    private volatile string? _cachedToken;
    private DateTimeOffset _tokenExpiry = DateTimeOffset.MinValue.AddSeconds(60); // FIX: avoid underflow on first AddSeconds(-30) check
    private readonly SemaphoreSlim _lock = new(1, 1);

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // FIX: compare against UtcNow directly without subtracting from MinValue
        if (string.IsNullOrEmpty(_cachedToken) || DateTimeOffset.UtcNow >= _tokenExpiry)
            await RefreshTokenAsync(cancellationToken);

        request.Headers.Authorization = new("Bearer", _cachedToken);
        return await base.SendAsync(request, cancellationToken);
    }

    private async Task RefreshTokenAsync(CancellationToken ct)
    {
        await _lock.WaitAsync(ct);
        try
        {
            // Double-check inside lock
            if (!string.IsNullOrEmpty(_cachedToken) && DateTimeOffset.UtcNow < _tokenExpiry) return;

            var o = opts.CurrentValue;
            var client = httpClientFactory.CreateClient("sabre-auth");
            var body = new FormUrlEncodedContent([
                new("grant_type", "client_credentials"),
                new("client_id", o.ClientId),
                new("client_secret", o.ClientSecret),
            ]);
            var resp = await client.PostAsync(o.TokenUrl, body, ct);
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadFromJsonAsync<SabreTokenResponse>(ct);
            _cachedToken = json!.AccessToken;

            // FIX: subtract 30s buffer safely — use a minimum expiry of 60s if ExpiresIn is too small
            var expiresIn = Math.Max(json.ExpiresIn - 30, 30);
            _tokenExpiry = DateTimeOffset.UtcNow.AddSeconds(expiresIn);

            logger.LogInformation("Sabre token refreshed, expires at {Expiry}", _tokenExpiry);
            // SECURITY: never log _cachedToken value
        }
        finally { _lock.Release(); }
    }
}
