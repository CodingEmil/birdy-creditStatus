using System.Text.Json;
using System.Text.Json.Nodes;

namespace BirdyCreditStatus.Core;

/// <summary>Liest das vorhandene Claude-Code-OAuth-Login desselben Users
/// (~/.claude/.credentials.json, Schlüssel claudeAiOauth). Schreibt nur bei Token-Rotation zurück.</summary>
public sealed class FileClaudeCredentialStore(string? pathOverride = null) : IClaudeCredentialStore
{
    private readonly string _path = pathOverride
        ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", ".credentials.json");

    public Task<IDisposable> AcquireRefreshLockAsync(CancellationToken cancellationToken = default) =>
        CredentialRefreshLock.AcquireFileAsync(_path, cancellationToken);

    public async Task<ClaudeOAuthCredentials?> LoadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(_path))
            {
                return null;
            }

            await using var stream = File.OpenRead(_path);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            if (!document.RootElement.TryGetProperty("claudeAiOauth", out var oauth))
            {
                return null;
            }

            if (!oauth.TryGetProperty("accessToken", out var access)
                || !oauth.TryGetProperty("refreshToken", out var refresh)
                || !oauth.TryGetProperty("expiresAt", out var expires))
            {
                return null;
            }

            var accessToken = access.GetString();
            var refreshToken = refresh.GetString();
            if (string.IsNullOrEmpty(accessToken) || string.IsNullOrEmpty(refreshToken))
            {
                return null;
            }

            return new ClaudeOAuthCredentials(accessToken, refreshToken, expires.GetInt64());
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

    public async Task SaveAsync(ClaudeOAuthCredentials credentials, CancellationToken cancellationToken = default)
    {
        string json;
        try
        {
            json = await File.ReadAllTextAsync(_path, cancellationToken);
        }
        catch (IOException)
        {
            return;
        }
        catch (UnauthorizedAccessException)
        {
            return;
        }

        JsonObject? root;
        try
        {
            root = JsonNode.Parse(json) as JsonObject;
        }
        catch (JsonException)
        {
            return;
        }

        if (root?["claudeAiOauth"] is not JsonObject oauth)
        {
            return;
        }

        oauth["accessToken"] = credentials.AccessToken;
        oauth["refreshToken"] = credentials.RefreshToken;
        oauth["expiresAt"] = credentials.ExpiresAtUnixMs;

        try
        {
            await File.WriteAllTextAsync(
                _path, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), cancellationToken);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
