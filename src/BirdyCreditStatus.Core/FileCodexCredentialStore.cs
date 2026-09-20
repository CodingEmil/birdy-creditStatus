using System.Text.Json;
using System.Text.Json.Nodes;

namespace BirdyCreditStatus.Core;

/// <summary>Liest die benutzerwählte Codex-Auth-Datei im OAuth-Format (D004, Default ~/.codex/auth.json,
/// Schlüssel tokens.access_token/refresh_token/account_id). Fehlende/korrupte Datei (oder fehlende
/// Tokens, z. B. reiner API-Key-Login) → null (Setup-Signal), nie Throw. Schreibt nur bei
/// Token-Rotation zurück und erhält dabei alle übrigen Felder.</summary>
public sealed class FileCodexCredentialStore(string? pathOverride = null) : ICodexCredentialStore
{
    private readonly string _path = pathOverride
        ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex", "auth.json");

    public async Task<CodexOAuthCredentials?> LoadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(_path))
            {
                return null;
            }

            await using var stream = File.OpenRead(_path);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            if (!document.RootElement.TryGetProperty("tokens", out var tokens)
                || tokens.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            if (!tokens.TryGetProperty("access_token", out var access)
                || !tokens.TryGetProperty("refresh_token", out var refresh))
            {
                return null;
            }

            var accessToken = access.GetString();
            var refreshToken = refresh.GetString();
            if (string.IsNullOrEmpty(accessToken) || string.IsNullOrEmpty(refreshToken))
            {
                return null;
            }

            string? accountId = null;
            if (tokens.TryGetProperty("account_id", out var account)
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

    public async Task SaveAsync(CodexOAuthCredentials credentials, CancellationToken cancellationToken = default)
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

        if (root?["tokens"] is not JsonObject tokens)
        {
            return;
        }

        tokens["access_token"] = credentials.AccessToken;
        tokens["refresh_token"] = credentials.RefreshToken;
        if (credentials.AccountId is not null)
        {
            tokens["account_id"] = credentials.AccountId;
        }

        root["last_refresh"] = DateTimeOffset.UtcNow.ToString("o");

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
