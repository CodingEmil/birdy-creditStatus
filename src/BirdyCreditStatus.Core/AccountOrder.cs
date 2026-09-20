namespace BirdyCreditStatus.Core;

/// <summary>Kartenreihenfolge (F007-T2, D012): Blöcke Claude → Codex → OpenCode Go,
/// innerhalb je Anlagereihenfolge (stabile Sortierung). Reines Mapping, UI und
/// Tests teilen sich diese Ordnung.</summary>
public static class AccountOrder
{
    private static int Rank(string provider) => provider switch
    {
        AccountProviders.Claude => 0,
        AccountProviders.Codex => 1,
        AccountProviders.OpenCodeGo => 2,
        _ => 3,
    };

    public static IReadOnlyList<Account> Sort(IEnumerable<Account> accounts) =>
        accounts
            .Select((account, index) => (account, index))
            .OrderBy(entry => Rank(entry.account.Provider))
            .ThenBy(entry => entry.index)
            .Select(entry => entry.account)
            .ToList();
}
