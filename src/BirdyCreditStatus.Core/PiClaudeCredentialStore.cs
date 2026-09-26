using System.Text.Json;

namespace BirdyCreditStatus.Core;

/// <summary>Liest das Claude-Login aus einer benannten Section einer Pi-Harness-Datei
/// (F011-T3, Mapping <c>access</c>/<c>refresh</c>/<c>expires</c> in Unix-Millisekunden,
/// analog zu <c>claudeAiOauth</c>). Fehlende Datei/Section, falsches Format, leere
/// Tokens → null, nie Throw. <c>SaveAsync</c> ist ein No-Op: die Pi-Datei wird nie
/// beschrieben (Pi refresht extern). Lock pro Datei+Section.</summary>
public sealed class PiClaudeCredentialStore(string? path, string? section) : IClaudeCredentialStore
{
    public Task<IDisposable> AcquireRefreshLockAsync(CancellationToken cancellationToken = default) =>
        CredentialRefreshLock.AcquirePiSectionAsync(path, section, cancellationToken);

    public async Task<ClaudeOAuthCredentials?> LoadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path)
                || string.IsNullOrWhiteSpace(section)
                || !File.Exists(path))
            {
                return null;
            }

            await using var stream = File.OpenRead(path);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty(section, out var sectionElement)
                || sectionElement.ValueKind != JsonValueKind.Object
                || !sectionElement.TryGetProperty("access", out var access)
                || !sectionElement.TryGetProperty("refresh", out var refresh)
                || !sectionElement.TryGetProperty("expires", out var expires))
            {
                return null;
            }

            var accessToken = access.ValueKind == JsonValueKind.String ? access.GetString() : null;
            var refreshToken = refresh.ValueKind == JsonValueKind.String ? refresh.GetString() : null;
            if (string.IsNullOrEmpty(accessToken)
                || string.IsNullOrEmpty(refreshToken)
                || !expires.TryGetInt64(out var expiresAtUnixMs))
            {
                return null;
            }

            return new ClaudeOAuthCredentials(accessToken, refreshToken, expiresAtUnixMs);
        }
        catch (IOException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    public Task SaveAsync(ClaudeOAuthCredentials credentials, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
