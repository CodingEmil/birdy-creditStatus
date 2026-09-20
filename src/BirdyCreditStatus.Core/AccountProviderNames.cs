namespace BirdyCreditStatus.Core;

/// <summary>Anzeigenamen der Anbieter (F007-T2): Titel-Badge je Karte, Dropdown im
/// Hinzufügen-Dialog (T3). Kennungen bleiben <see cref="AccountProviders"/>.</summary>
public static class AccountProviderNames
{
    public static string Display(string provider) => provider switch
    {
        AccountProviders.Claude => "Claude",
        AccountProviders.Codex => "Codex",
        AccountProviders.OpenCodeGo => "OpenCode Go",
        _ => "Codex",
    };

    public static IReadOnlyList<string> All { get; } =
        [AccountProviders.Claude, AccountProviders.Codex, AccountProviders.OpenCodeGo];

    /// <summary>Alter Einzelkarten-Cache-Schlüssel je Provider (F001–F003) für die
    /// migrationsfreie Übernahme auf den Kontonamen (F006-T2/F007-T2).</summary>
    public static string LegacyCacheKey(string provider) => provider switch
    {
        AccountProviders.Claude => "Claude",
        AccountProviders.OpenCodeGo => "OpenCode Go",
        _ => "Codex",
    };
}
