using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace BirdyCreditStatus.Core;

/// <summary>Echter OpenCode-Go-Adapter hinter <see cref="IQuotaAdapter"/> (D002/D005, F003-T2).
/// GET {base}/zen/go/v1/usage (Default-Base https://opencode.ai — per Prototyp gegen den
/// echten Key verifiziert, Befund als Kommentar an #12). Header: Bearer-API-Key,
/// Accept application/json, kein Inference-Nebeneffekt (passiver Leser). Mapping:
/// usage.rolling → Fenster Rolling, usage.weekly → Woche, usage.monthly → Monat,
/// percent (genutzt) → Rest (100 − genutzt, 0..100), resetsAt (ISO) → ResetsAt.
/// status ok und rate-limited zählen als Wert, fehlende Fenster werden weggelassen.
/// Keine Rotation (API-Key): fehlender Key und 401/403 → Setup-Signal, sonst n/a. Nie Throw.</summary>
public sealed class OpenCodeGoQuotaAdapter : IQuotaAdapter
{
    public const string DefaultBaseUrl = "https://opencode.ai";
    public const string UsagePath = "/zen/go/v1/usage";
    public const string NotAvailableMessage = "n/a – Key prüfen / offline";

    private readonly HttpClient _http;
    private readonly IOpenCodeGoCredentialStore _store;
    private readonly string _baseUrl;
    private readonly string _accountName;

    public OpenCodeGoQuotaAdapter(HttpClient http, IOpenCodeGoCredentialStore store, string? baseOverride = null, string accountName = "OpenCode Go")
    {
        _http = http;
        _store = store;
        _baseUrl = (baseOverride ?? DefaultBaseUrl).TrimEnd('/');
        _accountName = string.IsNullOrWhiteSpace(accountName) ? "OpenCode Go" : accountName;
    }

    public async Task<QuotaResult> FetchAsync(CancellationToken cancellationToken = default)
    {
        var apiKey = await _store.LoadAsync(cancellationToken);
        if (string.IsNullOrEmpty(apiKey))
        {
            return NotAvailable(loginRequired: true);
        }

        HttpResponseMessage response;
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, _baseUrl + UsagePath);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            response = await _http.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException)
        {
            return NotAvailable();
        }
        catch (TaskCanceledException)
        {
            return NotAvailable();
        }

        using (response)
        {
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                // Toter Key (keine Rotation bei API-Key): Setup-Hinweis, Key in Pi hinterlegen.
                return NotAvailable(loginRequired: true);
            }

            if (!response.IsSuccessStatusCode)
            {
                return NotAvailable();
            }

            List<QuotaWindow> windows;
            try
            {
                windows = await ParseUsageAsync(
                    await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken);
            }
            catch (JsonException)
            {
                return NotAvailable();
            }
            catch (IOException)
            {
                return NotAvailable();
            }

            if (windows.Count == 0)
            {
                return NotAvailable();
            }

            return new QuotaResult(true, new QuotaSnapshot(_accountName, windows, DateTimeOffset.Now));
        }
    }

    private static async Task<List<QuotaWindow>> ParseUsageAsync(Stream json, CancellationToken cancellationToken)
    {
        using var document = await JsonDocument.ParseAsync(json, cancellationToken: cancellationToken);
        if (!document.RootElement.TryGetProperty("usage", out var usage)
            || usage.ValueKind != JsonValueKind.Object)
        {
            return new List<QuotaWindow>();
        }

        var windows = new List<QuotaWindow>();
        TryAddWindow(usage, "rolling", "Rolling", windows);
        TryAddWindow(usage, "weekly", "Woche", windows);
        TryAddWindow(usage, "monthly", "Monat", windows);
        return windows;
    }

    private static void TryAddWindow(JsonElement usage, string jsonName, string windowName, List<QuotaWindow> windows)
    {
        if (!usage.TryGetProperty(jsonName, out var window)
            || window.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        if (!window.TryGetProperty("status", out var status)
            || status.ValueKind != JsonValueKind.String
            || status.GetString() is not ("ok" or "rate-limited"))
        {
            return;
        }

        if (!window.TryGetProperty("percent", out var percent)
            || percent.ValueKind != JsonValueKind.Number
            || !percent.TryGetDouble(out var usedPercent)
            || !double.IsFinite(usedPercent))
        {
            return;
        }

        DateTimeOffset? resetsAt = null;
        if (window.TryGetProperty("resetsAt", out var reset)
            && reset.ValueKind == JsonValueKind.String
            && DateTimeOffset.TryParse(
                reset.GetString(),
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var parsed))
        {
            resetsAt = parsed;
        }

        windows.Add(new QuotaWindow(windowName, Math.Clamp(100 - usedPercent, 0, 100), resetsAt));
    }

    private static QuotaResult NotAvailable(bool loginRequired = false) =>
        new(false, null, NotAvailableMessage, loginRequired);
}
