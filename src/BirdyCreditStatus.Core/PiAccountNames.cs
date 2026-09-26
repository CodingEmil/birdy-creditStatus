namespace BirdyCreditStatus.Core;

/// <summary>Editierbare Namensvorschläge für Pi-Konten (F011-T4, UI-frei): je Treffer
/// ein Vorschlag (<c>Pi Claude</c>, <c>Pi Codex</c>, <c>Pi Codex 2</c>, <c>Pi Go</c>;
/// sonstige Codex-Suffixe angehängt). Kollision mit bestehenden oder
/// Geschwister-Namen → hochzählen (ordinal wie <see cref="AccountStore"/>).
/// Reines Mapping, nie Throw.</summary>
public static class PiAccountNames
{
    private const string CodexSectionPrefix = "openai-codex";

    /// <summary>Vorschlag für einen Treffer ohne Kollisionsprüfung.</summary>
    public static string BaseName(string provider, string? section) => provider switch
    {
        AccountProviders.Claude => "Pi Claude",
        AccountProviders.OpenCodeGo => "Pi Go",
        _ => BaseCodexName(section),
    };

    /// <summary>Je ein Vorschlag je Treffer in Reihenfolge; bereits vergebene Namen
    /// (bestehend + Geschwister) werden hochgezählt. Nie Throw.</summary>
    public static IReadOnlyList<string> Suggest(
        IReadOnlyList<DetectedAuth>? hits,
        IEnumerable<string>? existingNames)
    {
        var taken = new HashSet<string>(existingNames ?? [], StringComparer.Ordinal);
        var suggestions = new List<string>();
        foreach (var hit in hits ?? [])
        {
            var name = Unique(BaseName(hit.Provider, hit.Section), taken);
            taken.Add(name);
            suggestions.Add(name);
        }

        return suggestions;
    }

    private static string BaseCodexName(string? section)
    {
        if (string.IsNullOrEmpty(section)
            || string.Equals(section, CodexSectionPrefix, StringComparison.Ordinal))
        {
            return "Pi Codex";
        }

        var rest = section.StartsWith(CodexSectionPrefix, StringComparison.Ordinal)
            ? section[CodexSectionPrefix.Length..].TrimStart('-', '_')
            : section;
        return string.IsNullOrEmpty(rest) ? "Pi Codex" : "Pi Codex " + rest;
    }

    private static string Unique(string baseName, HashSet<string> taken)
    {
        if (!taken.Contains(baseName))
        {
            return baseName;
        }

        // Vorschlag endet bereits auf " N" (z. B. Pi Codex 2): dort weiterzählen
        // statt "Pi Codex 2 2" anzuhängen.
        var prefix = baseName;
        var start = 2;
        var lastSpace = baseName.LastIndexOf(' ');
        if (lastSpace > 0
            && int.TryParse(baseName[(lastSpace + 1)..], out var trailing)
            && trailing >= 1)
        {
            prefix = baseName[..lastSpace];
            start = trailing + 1;
        }

        for (var i = start; ; i++)
        {
            var candidate = $"{prefix} {i}";
            if (!taken.Contains(candidate))
            {
                return candidate;
            }
        }
    }
}
