using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;

namespace TBE.HotelConnectorService.Application.Car;

public class AmadeusCarAuthHandler(
    IHttpClientFactory httpClientFactory,
    IOptionsMonitor<AmadeusCarOptions> opts,
    ILogger<AmadeusCarAuthHandler> logger) : DelegatingHandler
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
            if (DateTimeOffset.UtcNow < _tokenExpiry.AddSeconds(-30)) return;
            var o = opts.CurrentValue;
            var client = httpClientFactory.CreateClient("amadeus-car-auth");
            var body = new FormUrlEncodedContent([
                new("grant_type", "client_credentials"),
                new("client_id", o.ApiKey),
                new("client_secret", o.ApiSecret),
            ]);
            var resp = await client.PostAsync(o.TokenUrl, body, ct);
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadFromJsonAsync<AmadeusCarTokenResponse>(ct);
            _cachedToken = json!.AccessToken;
            _tokenExpiry = DateTimeOffset.UtcNow.AddSeconds(json.ExpiresIn);
            logger.LogInformation("Amadeus car token refreshed, expires at {Expiry}", _tokenExpiry);
            // SECURITY: token value never logged
        }
        finally { _lock.Release(); }
    }
}

internal sealed class AmadeusCarTokenResponse
{
    [System.Text.Json.Serialization.JsonPropertyName("access_token")] public string AccessToken { get; init; } = default!;
    [System.Text.Json.Serialization.JsonPropertyName("expires_in")]   public int ExpiresIn { get; init; }
}
