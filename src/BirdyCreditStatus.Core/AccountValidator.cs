using System.Text.Json;

namespace BirdyCreditStatus.Core;

/// <summary>Dialog-Validierung (F006-T3, je Provider-Format seit F007-T1, Pi-Sections
/// seit F011-T2): Kontoname (nicht leer, global über alle Anbieter eindeutig —
/// keine reservierten Namen mehr) und Auth-Datei je Provider-Format (nativ,
/// <c>section</c> null: Claude <c>claudeAiOauth</c> mit Tokens; Codex
/// <c>tokens.access_token</c> + <c>tokens.refresh_token</c>; OpenCode Go
/// <c>opencode-go.key</c>, nicht leer; Pi-Datei mit Section: die benannte Section
/// gegen Claude <c>access</c>/<c>refresh</c>/<c>expires</c>, Codex
/// <c>access</c>/<c>refresh</c>/<c>accountId</c> bzw. Go-<c>key</c>).
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
    public static string? ValidateAuthFile(string provider, string path) =>
        ValidateAuthFile(provider, path, null);

    /// <summary>Prüft die Auth-Datei mit optionaler Pi-Section (F011-T2):
/// <c>section</c> null = natives Verhalten unverändert (F007/F010 unberührt);
/// mit Section wird die benannte Pi-Section gegen das Provider-Format geprüft
/// (Namen aus <see cref="PiAuthDiscovery"/>, z. B. <c>anthropic</c>,
/// <c>openai-codex-2</c>). Fehlende Section, falsches Format, fehlende/korrupte
/// Datei = deutsche Meldung, nie Throw.</summary>
    public static string? ValidateAuthFile(string provider, string path, string? section)
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
                return FormatMessage(provider, section);
            }

            if (section is null)
            {
                var nativeOk = provider switch
                {
                    AccountProviders.Claude => HasClaudeTokens(root),
                    AccountProviders.OpenCodeGo => HasOpenCodeGoKey(root),
                    _ => HasCodexTokens(root),
                };
                return nativeOk ? null : FormatMessage(provider, null);
            }

            if (!root.TryGetProperty(section, out var sectionElement)
                || sectionElement.ValueKind != JsonValueKind.Object)
            {
                return FormatMessage(provider, section);
            }

            var piOk = provider switch
            {
                AccountProviders.Claude => HasPiClaudeTokens(sectionElement),
                AccountProviders.OpenCodeGo => HasPiGoKey(sectionElement),
                _ => HasPiCodexTokens(sectionElement),
            };
            return piOk ? null : FormatMessage(provider, section);
        }
        catch (JsonException)
        {
            return FormatMessage(provider, section);
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
        && access.ValueKind == JsonValueKind.String
        && refresh.ValueKind == JsonValueKind.String
        && access.GetString() is { Length: > 0 }
        && refresh.GetString() is { Length: > 0 };

    private static bool HasClaudeTokens(JsonElement root) =>
        root.TryGetProperty("claudeAiOauth", out var oauth)
        && oauth.ValueKind == JsonValueKind.Object
        && oauth.TryGetProperty("accessToken", out var access)
        && oauth.TryGetProperty("refreshToken", out var refresh)
        && oauth.TryGetProperty("expiresAt", out var expires)
        && expires.ValueKind == JsonValueKind.Number
        && access.ValueKind == JsonValueKind.String
        && refresh.ValueKind == JsonValueKind.String
        && access.GetString() is { Length: > 0 }
        && refresh.GetString() is { Length: > 0 };

    private static bool HasOpenCodeGoKey(JsonElement root) =>
        root.TryGetProperty("opencode-go", out var section)
        && section.ValueKind == JsonValueKind.Object
        && section.TryGetProperty("key", out var key)
        && key.ValueKind == JsonValueKind.String
        && !string.IsNullOrEmpty(key.GetString());

    private static bool HasPiCodexTokens(JsonElement section) =>
        section.TryGetProperty("access", out var access)
        && section.TryGetProperty("refresh", out var refresh)
        && section.TryGetProperty("accountId", out var accountId)
        && access.ValueKind == JsonValueKind.String
        && refresh.ValueKind == JsonValueKind.String
        && accountId.ValueKind == JsonValueKind.String
        && access.GetString() is { Length: > 0 }
        && refresh.GetString() is { Length: > 0 }
        && accountId.GetString() is { Length: > 0 };

    private static bool HasPiClaudeTokens(JsonElement section) =>
        section.TryGetProperty("access", out var access)
        && section.TryGetProperty("refresh", out var refresh)
        && section.TryGetProperty("expires", out var expires)
        && expires.ValueKind == JsonValueKind.Number
        && access.ValueKind == JsonValueKind.String
        && refresh.ValueKind == JsonValueKind.String
        && access.GetString() is { Length: > 0 }
        && refresh.GetString() is { Length: > 0 };

    private static bool HasPiGoKey(JsonElement section) =>
        section.TryGetProperty("key", out var key)
        && key.ValueKind == JsonValueKind.String
        && !string.IsNullOrEmpty(key.GetString());

    private static string FormatMessage(string provider, string? section) => section is null
        ? provider switch
        {
            AccountProviders.Claude =>
                "Datei enthält kein gültiges Claude-Login (claudeAiOauth mit Tokens) — bitte eine .credentials.json wählen.",
            AccountProviders.OpenCodeGo =>
                "Datei enthält keinen OpenCode-Go-Key (opencode-go.key) — bitte eine Auth-Datei mit Key wählen.",
            _ => "Datei enthält keine gültigen Tokens (access_token/refresh_token) — kein Plan-Kontingent lesbar.",
        }
        : provider switch
        {
            AccountProviders.Claude =>
                "Pi-Section enthält kein gültiges Claude-Login (access/refresh/expires) — bitte eine Pi-Datei mit anthropic-Login wählen.",
            AccountProviders.OpenCodeGo =>
                "Pi-Section enthält keinen OpenCode-Go-Key (key) — bitte eine Pi-Datei mit Key wählen.",
            _ => "Pi-Section enthält keine gültigen Tokens (access/refresh/accountId) — kein Plan-Kontingent lesbar.",
        };
}
