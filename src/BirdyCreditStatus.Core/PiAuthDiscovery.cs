using System.Text.Json;

namespace BirdyCreditStatus.Core;

/// <summary>Erkennung von Provider-Logins in einer Pi-Harness-Datei (F011-T1):
/// parst eine beliebig gewählte Datei im <c>auth.json</c>-Format und liefert je
/// mappbarer Section einen Kandidaten (1 Pfad → N Kandidaten). Mapping:
/// <c>anthropic</c> exakt → <c>claude</c>, <c>openai-codex*</c> per Präfix
/// (jedes Suffix = eigenes Konto) → <c>codex</c>, <c>opencode-go</c> exakt →
/// <c>opencode-go</c>. <c>openrouter</c>/Unbekanntes wird still ignoriert.
/// Fehlend/korrupt/unlesbar/kein Objekt = leere Liste, nie Throw (D003-Geist).
/// Read-only: die Datei wird nur gelesen. Keine Validierung der Token-Inhalte —
/// das ist Sache von <c>AccountValidator</c> (F011-T2).</summary>
public static class PiAuthDiscovery
{
    private const string AnthropicSection = "anthropic";
    private const string CodexSectionPrefix = "openai-codex";
    private const string OpenCodeGoSection = "opencode-go";

    /// <summary>Parst die Pi-Datei und liefert je mappbarer Section einen Kandidaten
    /// mit <c>Section</c> = originaler JSON-Property-Name. Nie Throw.</summary>
    public static IReadOnlyList<DetectedAuth> DetectFile(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return [];
        }

        try
        {
            using var stream = File.OpenRead(path);
            using var document = JsonDocument.Parse(stream);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return [];
            }

            var found = new List<DetectedAuth>();
            foreach (var property in root.EnumerateObject())
            {
                var provider = MapSection(property.Name);
                if (provider is null)
                {
                    continue;
                }

                found.Add(new DetectedAuth(
                    provider,
                    AccountProviderNames.Display(provider),
                    path,
                    property.Name));
            }

            return found;
        }
        catch (JsonException)
        {
            return [];
        }
        catch (IOException)
        {
            return [];
        }
        catch (UnauthorizedAccessException)
        {
            return [];
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return [];
        }
    }

    private static string? MapSection(string section) =>
        string.Equals(section, AnthropicSection, StringComparison.Ordinal) ? AccountProviders.Claude
        : section.StartsWith(CodexSectionPrefix, StringComparison.Ordinal) ? AccountProviders.Codex
        : string.Equals(section, OpenCodeGoSection, StringComparison.Ordinal) ? AccountProviders.OpenCodeGo
        : null;
}
