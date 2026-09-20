using System.Text.Json;

namespace BirdyCreditStatus.Core;

/// <summary>Dialog-Validierung (F006-T3, je Provider-Format seit F007-T1): Kontoname
/// (nicht leer, global über alle Anbieter eindeutig — keine reservierten Namen mehr)
/// und Auth-Datei je Provider-Format (Claude: <c>claudeAiOauth</c> mit Tokens;
/// Codex: <c>tokens.access_token</c> + <c>tokens.refresh_token</c>;
/// OpenCode Go: <c>opencode-go.key</c>, nicht leer).
/// Gibt bei Erfolg null zurück, sonst eine deutsche Meldung für den Dialog.
/// Wirft nie (fehlend/korrupt/unlesbar = Meldung).</summary>
public static class AccountValidator
{
    /// <summary>Prüft den Kontonamen gegen vorhandene Namen (ordinal, wie <see cref="AccountStore"/>).</summary>
    public static string? ValidateName(string name, IEnumerable<string> existingNames)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "Bitte einen Namen eingeben.";
        }

        var trimmed = name.Trim();
        foreach (var existing in existingNames)
        {
            if (existing == trimmed)
            {
                return $"„{trimmed}\" gibt es schon — bitte einen eindeutigen Namen wählen.";
            }
        }

        return null;
    }

    /// <summary>Prüft die Auth-Datei im Codex-Format (Alt-Pfad, entspricht Provider <c>codex</c>).</summary>
    public static string? ValidateAuthFile(string path) => ValidateAuthFile(AccountProviders.Codex, path);

    /// <summary>Prüft, ob die Auth-Datei lesbar ist und zum Provider-Format passt.
/// Reine API-Key-Dateien ohne Plan-Kontingent fallen bei Codex durch (n/a-Fall, D004).</summary>
    public static string? ValidateAuthFile(string provider, string path)
    {
        if (!AccountProviders.IsKnown(provider))
        {
            return "Unbekannter Anbieter — bitte einen der drei Anbieter wählen.";
        }

        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return "Auth-Datei nicht gefunden — bitte eine gültige Datei wählen.";
        }

        try
        {
            using var stream = File.OpenRead(path);
            using var document = JsonDocument.Parse(stream);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return FormatMessage(provider);
            }

            var ok = provider switch
            {
                AccountProviders.Claude => HasClaudeTokens(root),
                AccountProviders.OpenCodeGo => HasOpenCodeGoKey(root),
                _ => HasCodexTokens(root),
            };
            return ok ? null : FormatMessage(provider);
        }
        catch (JsonException)
        {
            return FormatMessage(provider);
        }
        catch (IOException)
        {
            return "Datei ist nicht lesbar — bitte Pfad und Rechte prüfen.";
        }
        catch (UnauthorizedAccessException)
        {
            return "Datei ist nicht lesbar — bitte Pfad und Rechte prüfen.";
        }
    }

    private static bool HasCodexTokens(JsonElement root) =>
        root.TryGetProperty("tokens", out var tokens)
        && tokens.ValueKind == JsonValueKind.Object
        && tokens.TryGetProperty("access_token", out var access)
        && tokens.TryGetProperty("refresh_token", out var refresh)
        && access.GetString() is { Length: > 0 }
        && refresh.GetString() is { Length: > 0 };

    private static bool HasClaudeTokens(JsonElement root) =>
        root.TryGetProperty("claudeAiOauth", out var oauth)
        && oauth.ValueKind == JsonValueKind.Object
        && oauth.TryGetProperty("accessToken", out var access)
        && oauth.TryGetProperty("refreshToken", out var refresh)
        && oauth.TryGetProperty("expiresAt", out var expires)
        && expires.ValueKind == JsonValueKind.Number
        && access.GetString() is { Length: > 0 }
        && refresh.GetString() is { Length: > 0 };

    private static bool HasOpenCodeGoKey(JsonElement root) =>
        root.TryGetProperty("opencode-go", out var section)
        && section.ValueKind == JsonValueKind.Object
        && section.TryGetProperty("key", out var key)
        && key.ValueKind == JsonValueKind.String
        && !string.IsNullOrEmpty(key.GetString());

    private static string FormatMessage(string provider) => provider switch
    {
        AccountProviders.Claude =>
            "Datei enthält kein gültiges Claude-Login (claudeAiOauth mit Tokens) — bitte eine .credentials.json wählen.",
        AccountProviders.OpenCodeGo =>
            "Datei enthält keinen OpenCode-Go-Key (opencode-go.key) — bitte eine Auth-Datei mit Key wählen.",
        _ => "Datei enthält keine gültigen Tokens (access_token/refresh_token) — kein Plan-Kontingent lesbar.",
    };
}
