using System.Text.Json;

namespace BirdyCreditStatus.Core;

/// <summary>Liest das Codex-Login aus einer benannten Section einer Pi-Harness-Datei
/// (F011-T3, Mapping <c>access</c>/<c>refresh</c>/<c>accountId</c> für den
/// ChatGPT-Account-Header; <c>accountId</c> optional wie natives <c>account_id</c>).
/// Fehlende Datei/Section, falsches Format, leere Tokens → null, nie Throw.
/// <c>SaveAsync</c> ist ein No-Op: die Pi-Datei wird nie beschrieben (Pi refresht
/// extern). Lock pro Datei+Section.</summary>
public sealed class PiCodexCredentialStore(string? path, string? section) : ICodexCredentialStore
{
    public Task<IDisposable> AcquireRefreshLockAsync(CancellationToken cancellationToken = default) =>
        CredentialRefreshLock.AcquirePiSectionAsync(path, section, cancellationToken);

    public async Task<CodexOAuthCredentials?> LoadAsync(CancellationToken cancellationToken = default)
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
                || !sectionElement.TryGetProperty("refresh", out var refresh))
            {
                return null;
            }

            var accessToken = access.ValueKind == JsonValueKind.String ? access.GetString() : null;
            var refreshToken = refresh.ValueKind == JsonValueKind.String ? refresh.GetString() : null;
            if (string.IsNullOrEmpty(accessToken) || string.IsNullOrEmpty(refreshToken))
            {
                return null;
            }

            string? accountId = null;
            if (sectionElement.TryGetProperty("accountId", out var account)
                && account.ValueKind == JsonValueKind.String)
            {
                accountId = account.GetString();
            }

            return new CodexOAuthCredentials(accessToken, refreshToken, accountId);
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

    public Task SaveAsync(CodexOAuthCredentials credentials, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
