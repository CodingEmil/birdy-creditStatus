namespace BirdyCreditStatus.Core;

/// <summary>Dialog-Defaults je Provider (F007-T3): vorausgefüllter Pfad, Dateidialog-Titel;
/// Startverzeichnis ist der Pfad-Ordner (fällt auf Userprofil zurück). Unbekannt → Codex.</summary>
public static class AccountProviderPaths
{
    public static string DefaultAuthPath(string provider)
    {
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Normalized(provider) switch
        {
            AccountProviders.Claude => Path.Combine(profile, ".claude", ".credentials.json"),
            AccountProviders.OpenCodeGo => Path.Combine(profile, ".pi", "agent", "auth.json"),
            _ => Path.Combine(profile, ".codex", "auth.json"),
        };
    }

    public static string InitialDirectory(string provider)
    {
        var directory = Path.GetDirectoryName(DefaultAuthPath(provider));
        return Directory.Exists(directory)
            ? directory!
            : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    }

    public static string FileDialogTitle(string provider) => Normalized(provider) switch
    {
        AccountProviders.Claude => "Claude-.credentials.json wählen",
        AccountProviders.OpenCodeGo => "OpenCode-Go-Auth-Datei wählen",
        _ => "Codex-Auth-Datei wählen",
    };

    private static string Normalized(string provider) =>
        AccountProviders.IsKnown(provider) ? provider : AccountProviders.Codex;
}
