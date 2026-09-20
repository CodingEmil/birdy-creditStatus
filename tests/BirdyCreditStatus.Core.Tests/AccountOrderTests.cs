namespace BirdyCreditStatus.Core.Tests;

/// <summary>Naht (F007-T2): Kontenliste in Blockreihenfolge Claude → Codex → OpenCode Go,
/// innen Anlagereihenfolge (stabil). Reines Sortier-Mapping für die Karten.</summary>
public sealed class AccountOrderTests
{
    [Fact]
    public void Mixed_accounts_sort_into_provider_blocks_stably()
    {
        var accounts = new List<Account>
        {
            new(AccountProviders.OpenCodeGo, "Go", "g.json"),
            new(AccountProviders.Codex, "Codex Arbeit", "a.json"),
            new(AccountProviders.Claude, "Claude", "c.json"),
            new(AccountProviders.Codex, "Codex", "b.json"),
        };

        var sorted = AccountOrder.Sort(accounts);

        Assert.Equal(["Claude", "Codex Arbeit", "Codex", "Go"], sorted.Select(a => a.Name));
    }

    [Fact]
    public void Display_names_cover_all_providers_with_codex_fallback()
    {
        Assert.Equal("Claude", AccountProviderNames.Display(AccountProviders.Claude));
        Assert.Equal("Codex", AccountProviderNames.Display(AccountProviders.Codex));
        Assert.Equal("OpenCode Go", AccountProviderNames.Display(AccountProviders.OpenCodeGo));
        Assert.Equal("Codex", AccountProviderNames.Display("fremd"));
        Assert.Equal(
            [AccountProviders.Claude, AccountProviders.Codex, AccountProviders.OpenCodeGo],
            AccountProviderNames.All);
        Assert.Equal("Claude", AccountProviderNames.LegacyCacheKey(AccountProviders.Claude));
        Assert.Equal("Codex", AccountProviderNames.LegacyCacheKey(AccountProviders.Codex));
        Assert.Equal("OpenCode Go", AccountProviderNames.LegacyCacheKey(AccountProviders.OpenCodeGo));
        Assert.Equal("Codex", AccountProviderNames.LegacyCacheKey("fremd"));
    }

    [Fact]
    public void Empty_and_single_provider_lists_keep_insertion_order()
    {
        Assert.Empty(AccountOrder.Sort([]));

        var codex = new List<Account>
        {
            new(AccountProviders.Codex, "b", "1.json"),
            new(AccountProviders.Codex, "a", "2.json"),
        };

        Assert.Equal(["b", "a"], AccountOrder.Sort(codex).Select(a => a.Name));
    }
}
