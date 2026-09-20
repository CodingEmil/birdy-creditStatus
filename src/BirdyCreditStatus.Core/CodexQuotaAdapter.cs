using System.Net.Http.Headers;
using System.Text.Json;

namespace BirdyCreditStatus.Core;

/// <summary>Echter Codex-Adapter hinter <see cref="IQuotaAdapter"/> (D002/D004, F002-T2).
/// GET {base}/wham/usage (Default-Base https://chatgpt.com/backend-api — per Prototyp gegen
/// CLI 0.155.1 verifiziert; die Ticket-These /api/codex/usage liefert live 404). Header:
/// Bearer-Access-Token, ChatGPT-Account-ID, User-Agent codex-cli (ohne UA blockt die WAF),
/// kein luna-reserve-Header (passiver Leser). Mapping: Bucket limit_id == "codex"
/// (primäres rate_limit), Fallback erster Eintrag; primary → Fenster 5 Stunden,
/// secondary → Fenster Woche, used_percent → Rest (100 − genutzt, 0..100),
/// reset_at (Unix-Sekunden) → ResetsAt. Zeitstempel ist die Abrufzeit.
/// Rotation bei Ablauf (401 → POST https://auth.openai.com/oauth/token, JSON-Grant mit
/// Client-ID aus dem CLI-Source) mit einem Retry; tote Credentials (invalid_grant) und
/// fehlende Datei → Setup-Signal, sonst n/a. Nie Throw.</summary>
public sealed class CodexQuotaAdapter : IQuotaAdapter
{
    public const string DefaultBaseUrl = "https://chatgpt.com/backend-api";
    public const string UsagePath = "/wham/usage";
    public const string TokenEndpoint = "https://auth.openai.com/oauth/token";
    public const string ClientId = "app_EMoamEEZ73f0CkXaXp7hrann";
    public const string NotAvailableMessage = "n/a – Key prüfen / offline";

    private readonly HttpClient _http;
    private readonly ICodexCredentialStore _store;
    private readonly string _baseUrl;
    private readonly string _tokenEndpoint;
    private readonly string _accountName;

    public CodexQuotaAdapter(
        HttpClient http,
        ICodexCredentialStore store,
        string? baseOverride = null,
        string? tokenEndpointOverride = null,
        string accountName = "Codex")
    {
        _http = http;
        _store = store;
        _baseUrl = (baseOverride ?? DefaultBaseUrl).TrimEnd('/');
        _tokenEndpoint = tokenEndpointOverride ?? TokenEndpoint;
        _accountName = string.IsNullOrWhiteSpace(accountName) ? "Codex" : accountName;
    }

    public async Task<QuotaResult> FetchAsync(CancellationToken cancellationToken = default)
    {
        var credentials = await _store.LoadAsync(cancellationToken);
        if (credentials is null)
        {
            return NotAvailable(loginRequired: true);
        }

        var response = await GetUsageAsync(credentials, cancellationToken);
        if (response is null)
        {
            return NotAvailable();
        }

        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            response.Dispose();
            var (refreshed, dead) = await RefreshAsync(credentials, cancellationToken);
            if (dead)
            {
                return NotAvailable(loginRequired: true);
            }

            if (refreshed is null)
            {
                return NotAvailable();
            }

            credentials = refreshed;
            await _store.SaveAsync(credentials, cancellationToken);
            response = await GetUsageAsync(credentials, cancellationToken);
            if (response is null)
            {
                return NotAvailable();
            }
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                return NotAvailable();
            }

            var windows = await ParseSnapshotsAsync(
                await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken);
            if (windows.Count == 0)
            {
                return NotAvailable();
            }

            return new QuotaResult(true, new QuotaSnapshot(_accountName, windows, DateTimeOffset.Now));
        }
    }

    private async Task<HttpResponseMessage?> GetUsageAsync(
        CodexOAuthCredentials credentials, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, _baseUrl + UsagePath);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", credentials.AccessToken);
            if (!string.IsNullOrEmpty(credentials.AccountId))
            {
                request.Headers.TryAddWithoutValidation("ChatGPT-Account-ID", credentials.AccountId);
            }

            request.Headers.TryAddWithoutValidation("User-Agent", "codex-cli");

            return await _http.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (TaskCanceledException)
        {
            return null;
        }
    }

    /// <summary>Rotiert per Refresh-Token. Credentials null + dead false = transient (n/a),
    /// dead true = dauerhaft tote Credentials (Setup-Signal).</summary>
    private async Task<(CodexOAuthCredentials? Credentials, bool Dead)> RefreshAsync(
        CodexOAuthCredentials credentials, CancellationToken cancellationToken)
    {
        try
        {
            var payload = JsonSerializer.Serialize(new
            {
                grant_type = "refresh_token",
                client_id = ClientId,
                refresh_token = credentials.RefreshToken,
            });
            using var request = new HttpRequestMessage(HttpMethod.Post, _tokenEndpoint)
            {
                Content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json"),
            };

            using var response = await _http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return (null, response.StatusCode is System.Net.HttpStatusCode.BadRequest
                    or System.Net.HttpStatusCode.Unauthorized);
            }

            using var document = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
            var root = document.RootElement;
            if (!root.TryGetProperty("access_token", out var accessElement))
            {
                return (null, false);
            }

            var accessToken = accessElement.GetString();
            if (string.IsNullOrEmpty(accessToken))
            {
                return (null, false);
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

            return (new CodexOAuthCredentials(accessToken, refreshToken, credentials.AccountId), false);
        }
        catch (HttpRequestException)
        {
            return (null, false);
        }
        catch (TaskCanceledException)
        {
            return (null, false);
        }
        catch (JsonException)
        {
            return (null, false);
        }
    }

    private static async Task<List<QuotaWindow>> ParseSnapshotsAsync(
        Stream json, CancellationToken cancellationToken)
    {
        var entries = new List<(string LimitId, List<QuotaWindow> Windows)>();
        try
        {
            using var document = await JsonDocument.ParseAsync(json, cancellationToken: cancellationToken);
            var root = document.RootElement;

            if (root.TryGetProperty("rate_limit", out var rateLimit)
                && rateLimit.ValueKind == JsonValueKind.Object)
            {
                entries.Add(("codex", ParseWindows(rateLimit)));
            }

            if (root.TryGetProperty("additional_rate_limits", out var additional)
                && additional.ValueKind == JsonValueKind.Array)
            {
                foreach (var entry in additional.EnumerateArray())
                {
                    if (entry.ValueKind != JsonValueKind.Object)
                    {
                        continue;
                    }

                    string? limitId = null;
                    if (entry.TryGetProperty("metered_feature", out var feature)
                        && feature.ValueKind == JsonValueKind.String)
                    {
                        limitId = feature.GetString();
                    }

                    limitId ??= entry.TryGetProperty("limit_name", out var name)
                        && name.ValueKind == JsonValueKind.String
                        ? name.GetString()
                        : null;

                    if (!entry.TryGetProperty("rate_limit", out var nested)
                        || nested.ValueKind != JsonValueKind.Object)
                    {
                        continue;
                    }

                    var windows = ParseWindows(nested);
                    if (windows.Count > 0)
                    {
                        entries.Add((limitId ?? string.Empty, windows));
                    }
                }
            }
        }
        catch (JsonException)
        {
            return new List<QuotaWindow>();
        }
        catch (IOException)
        {
            return new List<QuotaWindow>();
        }

        var selected = entries.FirstOrDefault(e => e.LimitId == "codex");
        if (selected == default)
        {
            selected = entries.FirstOrDefault();
        }

        return selected == default ? new List<QuotaWindow>() : selected.Windows;
    }

    private static List<QuotaWindow> ParseWindows(JsonElement rateLimit)
    {
        var windows = new List<QuotaWindow>();

        if (rateLimit.TryGetProperty("primary_window", out var primary)
            && primary.ValueKind == JsonValueKind.Object
            && TryParseWindow(primary, out var fiveHour))
        {
            windows.Add(new QuotaWindow("5 Stunden", fiveHour.Percent, fiveHour.ResetsAt));
        }

        if (rateLimit.TryGetProperty("secondary_window", out var secondary)
            && secondary.ValueKind == JsonValueKind.Object
            && TryParseWindow(secondary, out var week))
        {
            windows.Add(new QuotaWindow("Woche", week.Percent, week.ResetsAt));
        }

        return windows;
    }

    private static bool TryParseWindow(JsonElement window, out (double Percent, DateTimeOffset? ResetsAt) result)
    {
        result = default;
        if (!window.TryGetProperty("used_percent", out var used)
            || used.ValueKind != JsonValueKind.Number
            || !used.TryGetDouble(out var usedPercent)
            || !double.IsFinite(usedPercent))
        {
            return false;
        }

        DateTimeOffset? resetsAt = null;
        if (window.TryGetProperty("reset_at", out var reset)
            && reset.ValueKind == JsonValueKind.Number
            && reset.TryGetInt64(out var resetSeconds))
        {
            try
            {
                resetsAt = DateTimeOffset.FromUnixTimeSeconds(resetSeconds);
            }
            catch (ArgumentOutOfRangeException)
            {
            }
        }

        result = (Math.Clamp(100 - usedPercent, 0, 100), resetsAt);
        return true;
    }

    private static QuotaResult NotAvailable(bool loginRequired = false) =>
        new(false, null, NotAvailableMessage, loginRequired);
}
