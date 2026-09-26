using BirdyCreditStatus.Core;

namespace BirdyCreditStatus.Core.Tests;

/// <summary>Naht (F011-T4): Pi-Namensvorschläge — je Treffer ein editierbarer Vorschlag,
/// Kollision → hochzählen. UI-frei, nie Throw.</summary>
public sealed class PiAccountNamesTests
{
    private static DetectedAuth Hit(string provider, string section, string path = "C:\\pi\\auth.json") =>
        new(provider, AccountProviderNames.Display(provider), path, section);

    [Fact]
    public void Canonical_sections_yield_canonical_suggestions_in_order()
    {
        var hits = new[]
        {
            Hit(AccountProviders.Claude, "anthropic"),
            Hit(AccountProviders.Codex, "openai-codex"),
            Hit(AccountProviders.Codex, "openai-codex-2"),
            Hit(AccountProviders.OpenCodeGo, "opencode-go"),
        };

        var names = PiAccountNames.Suggest(hits, []);

        Assert.Equal(["Pi Claude", "Pi Codex", "Pi Codex 2", "Pi Go"], names);
    }

    [Fact]
    public void Custom_codex_suffix_is_appended()
    {
        var names = PiAccountNames.Suggest([Hit(AccountProviders.Codex, "openai-codex-arbeit")], []);

        Assert.Equal(["Pi Codex arbeit"], names);
    }

    [Fact]
    public void Collision_with_existing_names_counts_up()
    {
        var hits = new[]
        {
            Hit(AccountProviders.Codex, "openai-codex"),
            Hit(AccountProviders.Codex, "openai-codex-2"),
        };

        Assert.Equal(
            ["Pi Codex 3", "Pi Codex 4"],
            PiAccountNames.Suggest(hits, ["Pi Codex", "Pi Codex 2"]));
        Assert.Equal(
            ["Pi Codex 2"],
            PiAccountNames.Suggest(hits[..1], ["Pi Codex"]));
    }

    [Fact]
    public void Sibling_suggestions_never_collide_without_existing_names()
    {
        var hits = new[]
        {
            Hit(AccountProviders.Codex, "openai-codex"),
            Hit(AccountProviders.Codex, "openai-codex-2"),
        };

        var names = PiAccountNames.Suggest(hits, []);

        Assert.Equal(2, names.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Empty_hits_yield_empty_without_throw()
    {
        Assert.Empty(PiAccountNames.Suggest([], ["Pi Claude"]));
        Assert.Empty(PiAccountNames.Suggest(null!, null!));
    }
}
