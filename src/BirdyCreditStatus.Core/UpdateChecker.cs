using System.Text.Json;

namespace BirdyCreditStatus.Core;

/// <summary>Gefundenes Release: Version + Setup-Download-URL.</summary>
public sealed record ReleaseInfo(Version Version, string DownloadUrl, string Tag);

/// <summary>Update-Prüfergebnis (F009-T1): Fund, aktuell oder nicht prüfbar — nie Throw.</summary>
public abstract record UpdateCheckResult
{
    public sealed record Available(ReleaseInfo Release) : UpdateCheckResult;

    public sealed record Current(Version Running) : UpdateCheckResult;

    public sealed record Unavailable(string Reason) : UpdateCheckResult;
}

/// <summary>App-Update-Check gegen GitHub Releases (F009-T1): <c>releases/latest</c>
/// ohne Token (öffentlich), Tag <c>vX.Y.Z</c> gegen laufende Version, Setup-Asset
/// passend zum Tag (sonst erstes <c>.exe</c>). Download streamt in eine Datei.
/// Alles wirft nie (offline/kaputt = <c>Unavailable</c>).</summary>
public sealed class UpdateChecker
{
    public const string DefaultRepository = "CodingEmil/birdy-creditStatus";

    private readonly HttpClient _http;
    private readonly string _repository;
    private readonly string? _apiBaseOverride;

    public UpdateChecker(HttpClient http, string repository = DefaultRepository, string? apiBaseOverride = null)
    {
        _http = http;
        _repository = string.IsNullOrWhiteSpace(repository) ? DefaultRepository : repository;
        _apiBaseOverride = apiBaseOverride;
    }

    public async Task<UpdateCheckResult> CheckAsync(Version current, CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Get, $"{ApiBase()}/repos/{_repository}/releases/latest");
            request.Headers.TryAddWithoutValidation("User-Agent", "birdy-creditStatus");
            request.Headers.TryAddWithoutValidation("Accept", "application/vnd.github+json");

            using var response = await _http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return new UpdateCheckResult.Unavailable(
                    $"Release-Abfrage meldet {(int)response.StatusCode} — ggf. offline oder noch kein Release.");
            }

            var release = await ParseLatestAsync(
                await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken);
            if (release is null)
            {
                return new UpdateCheckResult.Unavailable("Release-Antwort unverständlich oder ohne Setup-Datei.");
            }

            return release.Version > current
                ? new UpdateCheckResult.Available(release)
                : new UpdateCheckResult.Current(current);
        }
        catch (HttpRequestException)
        {
            return new UpdateCheckResult.Unavailable("Update-Server nicht erreichbar — ggf. offline.");
        }
        catch (TaskCanceledException)
        {
            return new UpdateCheckResult.Unavailable("Update-Abfrage abgebrochen (Timeout?).");
        }
        catch (JsonException)
        {
            return new UpdateCheckResult.Unavailable("Release-Antwort unverständlich.");
        }
        catch (IOException)
        {
            return new UpdateCheckResult.Unavailable("Update-Antwort nicht lesbar.");
        }
    }

    public async Task DownloadAsync(string url, string destPath, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
        using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var target = File.Create(destPath);
        await source.CopyToAsync(target, cancellationToken);
    }

    private string ApiBase() => (_apiBaseOverride ?? "https://api.github.com").TrimEnd('/');

    private static async Task<ReleaseInfo?> ParseLatestAsync(Stream json, CancellationToken cancellationToken)
    {
        try
        {
            using var document = await JsonDocument.ParseAsync(json, cancellationToken: cancellationToken);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("tag_name", out var tagElement)
                || tagElement.ValueKind != JsonValueKind.String
                || !TryParseVersion(tagElement.GetString(), out var version))
            {
                return null;
            }

            string? expected = null;
            string? fallback = null;
            if (root.TryGetProperty("assets", out var assets)
                && assets.ValueKind == JsonValueKind.Array)
            {
                foreach (var asset in assets.EnumerateArray())
                {
                    if (asset.ValueKind != JsonValueKind.Object
                        || !asset.TryGetProperty("name", out var nameElement)
                        || !asset.TryGetProperty("browser_download_url", out var urlElement)
                        || nameElement.ValueKind != JsonValueKind.String
                        || urlElement.ValueKind != JsonValueKind.String)
                    {
                        continue;
                    }

                    var name = nameElement.GetString();
                    var url = urlElement.GetString();
                    if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(url))
                    {
                        continue;
                    }

                    if (name == $"birdy-creditStatus-Setup-{version}.exe")
                    {
                        expected = url;
                        break;
                    }

                    fallback ??= name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? url : null;
                }
            }

            var download = expected ?? fallback;
            return download is null ? null : new ReleaseInfo(version, download, tagElement.GetString()!);
        }
        catch (JsonException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    private static bool TryParseVersion(string? tag, out Version version)
    {
        version = new Version(0, 0, 0);
        if (string.IsNullOrWhiteSpace(tag))
        {
            return false;
        }

        var numeric = tag.Trim().TrimStart('v', 'V');
        return Version.TryParse(numeric, out version!);
    }
}
