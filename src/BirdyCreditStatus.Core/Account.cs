namespace BirdyCreditStatus.Core;

/// <summary>Anbieter-Kennungen für Konten (F007, D012): klein, stabil, serialisiert in
/// <c>accounts.json</c>. Anzeigenamen („Claude", „Codex", „OpenCode Go") sind davon
/// unabhängig und nur Migrations-Defaults.</summary>
public static class AccountProviders
{
    public const string Claude = "claude";
    public const string Codex = "codex";
    public const string OpenCodeGo = "opencode-go";

    public static bool IsKnown(string? provider) =>
        provider is Claude or Codex or OpenCodeGo;
}

/// <summary>Konto-Naht (F006, verallgemeinert F007, F011-T2, D003-Geist): ein Konto ist ein
/// Quadrupel aus Anbieter, frei wählbarem global eindeutigem Namen, Auth-Datei
/// (wiederverwendeter CLI-Login, z. B. Zweitlogin per CODEX_HOME bzw. Dateikopie)
/// und optionaler Pi-Section (<c>AuthSection</c>, <c>null</c> = native Datei,
/// gesetzt = Treffer aus einer Pi-Datei, gleiche Datei für mehrere Konten erlaubt).
/// In v1 kein eigener OAuth-Flow in der App.</summary>
public sealed record Account(string Provider, string Name, string AuthFilePath, string? AuthSection = null);
