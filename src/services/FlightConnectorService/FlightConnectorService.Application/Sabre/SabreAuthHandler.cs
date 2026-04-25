using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;

namespace TBE.FlightConnectorService.Application.Sabre;

public class SabreAuthHandler(
    IHttpClientFactory httpClientFactory,
    IOptionsMonitor<SabreOptions> opts,
    ILogger<SabreAuthHandler> logger) : DelegatingHandler
{
    private volatile string? _cachedToken;
    private DateTimeOffset _tokenExpiry = DateTimeOffset.MinValue.AddSeconds(60);
    private readonly SemaphoreSlim _lock = new(1, 1);

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_cachedToken) || DateTimeOffset.UtcNow >= _tokenExpiry)
            await RefreshTokenAsync(cancellationToken);

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _cachedToken);
        return await base.SendAsync(request, cancellationToken);
    }

    private async Task RefreshTokenAsync(CancellationToken ct)
    {
        await _lock.WaitAsync(ct);
        try
        {
            if (!string.IsNullOrEmpty(_cachedToken) && DateTimeOffset.UtcNow < _tokenExpiry) return;

            var o = opts.CurrentValue;
            var client = httpClientFactory.CreateClient("sabre-auth");

            // Sabre requires HTTP Basic Auth: Base64(clientId:clientSecret)
            var credentials = Convert.ToBase64String(
                Encoding.UTF8.GetBytes($"{o.ClientId}:{o.ClientSecret}"));

            var req = new HttpRequestMessage(HttpMethod.Post, o.TokenUrl);
            req.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
            req.Content = new FormUrlEncodedContent([
                new("grant_type", "client_credentials")
            ]);

            var resp = await client.SendAsync(req, ct);
            resp.EnsureSuccessStatusCode();

            var json = await resp.Content.ReadFromJsonAsync<SabreTokenResponse>(ct);
            _cachedToken = json!.AccessToken;

            var expiresIn = Math.Max(json.ExpiresIn - 30, 30);
            _tokenExpiry = DateTimeOffset.UtcNow.AddSeconds(expiresIn);

            logger.LogInformation("Sabre token refreshed, expires at {Expiry}", _tokenExpiry);
            // SECURITY: never log _cachedToken value
        }
        finally { _lock.Release(); }
    }
}
