using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;

namespace BirdyCreditStatus.Core;

/// <summary>Echter Claude-Adapter hinter <see cref="IQuotaAdapter"/> (D002, F001-T2).
/// GET api.anthropic.com/api/oauth/usage mit Beta oauth-2025-04-20; Mapping limits[]:
/// kind session → Fenster Session, kind weekly_all → Fenster Woche, percent → Rest (100 − genutzt, auf 0..100 begrenzt).
/// Fehler (401, offline, timeout, fehlendes Login) werden als n/a-Ergebnis sichtbar, nie als Throw.</summary>
public sealed class ClaudeQuotaAdapter : IQuotaAdapter
{
    public const string UsageEndpoint = "https://api.anthropic.com/api/oauth/usage";
    public const string TokenEndpoint = "https://platform.claude.com/v1/oauth/token";
    public const string ClientId = "9d1c250a-e61b-44d9-88ed-5944d1962f5e";
    public const string Scopes = "user:profile user:inference user:sessions:claude_code user:mcp_servers user:file_upload";
    public const string NotAvailableMessage = "n/a – Key prüfen / offline";

    private static readonly TimeSpan RefreshSkew = TimeSpan.FromMinutes(1);

    private readonly HttpClient _http;
    private readonly IClaudeCredentialStore _store;
    private readonly string _tokenEndpoint;
    private readonly string _accountName;

    public ClaudeQuotaAdapter(HttpClient http, IClaudeCredentialStore store, string? tokenEndpointOverride = null, string accountName = "Claude")
    {
        _http = http;
        _store = store;
        _tokenEndpoint = tokenEndpointOverride ?? TokenEndpoint;
        _accountName = string.IsNullOrWhiteSpace(accountName) ? "Claude" : accountName;
    }

    public async Task<QuotaResult> FetchAsync(CancellationToken cancellationToken = default)
    {
        var credentials = await _store.LoadAsync(cancellationToken);
        if (credentials is null)
        {
            return NotAvailable(loginRequired: true);
        }

        credentials = await EnsureFreshTokenAsync(credentials, cancellationToken);
        if (credentials is null)
        {
            return NotAvailable();
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, UsageEndpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", credentials.AccessToken);
            request.Headers.Add("anthropic-beta", "oauth-2025-04-20");

            using var response = await _http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return NotAvailable();
            }

            var windows = await ParseLimitsAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken);
            if (windows.Count == 0)
            {
                return NotAvailable();
            }

            return new QuotaResult(true, new QuotaSnapshot(_accountName, windows, DateTimeOffset.Now));
        }
        catch (HttpRequestException)
        {
            return NotAvailable();
        }
        catch (TaskCanceledException)
        {
            return NotAvailable();
        }
        catch (JsonException)
        {
            return NotAvailable();
        }
        catch (IOException)
        {
            return NotAvailable();
        }
    }

    private async Task<ClaudeOAuthCredentials?> EnsureFreshTokenAsync(
        ClaudeOAuthCredentials credentials, CancellationToken cancellationToken)
    {
        if (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() < credentials.ExpiresAtUnixMs - (long)RefreshSkew.TotalMilliseconds)
        {
            return credentials;
        }

        try
        {
            var payload = JsonSerializer.Serialize(new
            {
                grant_type = "refresh_token",
                refresh_token = credentials.RefreshToken,
                client_id = ClientId,
                scope = Scopes,
            });
            using var request = new HttpRequestMessage(HttpMethod.Post, _tokenEndpoint)
            {
                Content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json"),
            };

            using var response = await _http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            using var document = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
            var root = document.RootElement;
            if (!root.TryGetProperty("access_token", out var accessElement)
                || !root.TryGetProperty("expires_in", out var expiresElement))
            {
                return null;
            }

            var accessToken = accessElement.GetString();
            if (string.IsNullOrEmpty(accessToken))
            {
                return null;
            }

            var expiresInSeconds = expiresElement.GetDouble();
            if (!double.IsFinite(expiresInSeconds) || expiresInSeconds <= 0)
            {
                return null;
            }

            var refreshToken = credentials.RefreshToken;
            if (root.TryGetProperty("refresh_token", out var refreshElement))
            {
                var rotated = refreshElement.GetString();
                if (!string.IsNullOrEmpty(rotated))
                {
                    refreshToken = rotated;
                }
            }

            var refreshed = new ClaudeOAuthCredentials(
                accessToken,
                refreshToken,
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + (long)(expiresInSeconds * 1000));

            await _store.SaveAsync(refreshed, cancellationToken);
            return refreshed;
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (TaskCanceledException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static async Task<List<QuotaWindow>> ParseLimitsAsync(Stream json, CancellationToken cancellationToken)
    {
        var windows = new List<QuotaWindow>();
        using var document = await JsonDocument.ParseAsync(json, cancellationToken: cancellationToken);
        if (!document.RootElement.TryGetProperty("limits", out var limits)
            || limits.ValueKind != JsonValueKind.Array)
        {
            return windows;
        }

        double? session = null;
        DateTimeOffset? sessionReset = null;
        double? week = null;
        DateTimeOffset? weekReset = null;
        foreach (var entry in limits.EnumerateArray())
        {
            if (!entry.TryGetProperty("kind", out var kindElement)
                || !entry.TryGetProperty("percent", out var percentElement))
            {
                continue;
            }

            var kind = kindElement.GetString();
            var percent = percentElement.GetDouble();
            var reset = ParseReset(entry);
            if (kind == "session")
            {
                session = percent;
                sessionReset = reset;
            }
            else if (kind == "weekly_all")
            {
                week = percent;
                weekReset = reset;
            }
        }

        if (session.HasValue)
        {
            windows.Add(new QuotaWindow("Session", ToRemaining(session.Value), sessionReset));
        }

        if (week.HasValue)
        {
            windows.Add(new QuotaWindow("Woche", ToRemaining(week.Value), weekReset));
        }

        return windows;
    }

    private static double ToRemaining(double percentUsed) => Math.Clamp(100 - percentUsed, 0, 100);

    private static DateTimeOffset? ParseReset(JsonElement entry)
    {
        if (!entry.TryGetProperty("resets_at", out var resetElement)
            || resetElement.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return DateTimeOffset.TryParse(
            resetElement.GetString(),
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out var resetsAt)
            ? resetsAt
            : null;
    }

    private static QuotaResult NotAvailable(bool loginRequired = false) =>
        new(false, null, NotAvailableMessage, loginRequired);
}
