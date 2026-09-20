namespace BirdyCreditStatus.Core;

/// <summary>Laufzeit-Versionsanzeige (Popup-Footer): formatiert die Assembly-Version
/// der App als „vX.Y.Z\" (Major.Minor.Patch). Null → Fallback-Text, nie Throw.</summary>
public static class AppVersion
{
    public static string Format(Version? version) =>
        version is null
            ? "Version unbekannt"
            : $"v{version.Major}.{version.Minor}.{Math.Max(version.Build, 0)}";
}
