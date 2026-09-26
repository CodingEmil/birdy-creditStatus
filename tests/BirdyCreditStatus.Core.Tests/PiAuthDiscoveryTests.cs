using BirdyCreditStatus.Core;

namespace BirdyCreditStatus.Core.Tests;

/// <summary>Naht (F011-T1): Pi-Datei-Erkennung — je mappbarer Section ein Treffer,
/// nummerierte openai-codex* einzeln, openrouter/Unbekannt raus, korrupt/fehlend = leer, nie Throw.</summary>
public sealed class PiAuthDiscoveryTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

    public void Dispose()
    {
        try
        {
            Directory.Delete(_directory, true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    [Fact]
    public void Four_mappable_sections_yield_four_candidates_with_sections()
    {
        var path = WriteAuthFile("auth.json", """
            {
                "anthropic": {"type": "oauth"},
                "openai-codex": {"type": "oauth"},
                "openai-codex-2": {"type": "oauth"},
                "opencode-go": {"type": "api_key", "key": "go-1"}
            }
            """);

        var found = PiAuthDiscovery.DetectFile(path);

        Assert.Equal(4, found.Count);
        Assert.All(found, c => Assert.Equal(path, c.Path));
        Assert.Equal(
            ["anthropic", "openai-codex", "openai-codex-2", "opencode-go"],
            found.Select(c => c.Section));
        Assert.Equal(
            [AccountProviders.Claude, AccountProviders.Codex, AccountProviders.Codex, AccountProviders.OpenCodeGo],
            found.Select(c => c.Provider));
    }

    [Fact]
    public void Numbered_codex_sections_are_individual_accounts()
    {
        var path = WriteAuthFile("auth.json", """
            {"openai-codex": {}, "openai-codex-2": {}, "openai-codex-arbeit": {}}
            """);

        var found = PiAuthDiscovery.DetectFile(path);

        Assert.Equal(3, found.Count);
        Assert.All(found, c => Assert.Equal(AccountProviders.Codex, c.Provider));
        Assert.Equal(
            ["openai-codex", "openai-codex-2", "openai-codex-arbeit"],
            found.Select(c => c.Section));
    }

    [Fact]
    public void Openrouter_and_unknown_sections_are_excluded()
    {
        var path = WriteAuthFile("auth.json", """
            {"anthropic": {}, "openrouter": {"key": "x"}, "fremd": {}}
            """);

        var found = PiAuthDiscovery.DetectFile(path);

        var single = Assert.Single(found);
        Assert.Equal(AccountProviders.Claude, single.Provider);
        Assert.Equal("anthropic", single.Section);
    }

    [Fact]
    public void Only_unsupported_sections_yield_empty()
    {
        var path = WriteAuthFile("auth.json", """{"openrouter": {"key": "x"}}""");

        Assert.Empty(PiAuthDiscovery.DetectFile(path));
    }

    [Fact]
    public void Display_names_follow_provider_display()
    {
        var path = WriteAuthFile("auth.json", """
            {"anthropic": {}, "openai-codex": {}, "opencode-go": {}}
            """);

        var found = PiAuthDiscovery.DetectFile(path);

        Assert.Equal(
            ["Claude", "Codex", "OpenCode Go"],
            found.Select(c => c.DisplayName));
    }

    [Fact]
    public void Missing_file_yields_empty_without_throw()
    {
        var missing = Path.Combine(_directory, "fehlt.json");

        Assert.Empty(PiAuthDiscovery.DetectFile(missing));
        Assert.Empty(PiAuthDiscovery.DetectFile(""));
        Assert.Empty(PiAuthDiscovery.DetectFile("   "));
        Assert.Empty(PiAuthDiscovery.DetectFile(null));
    }

    [Fact]
    public void Corrupt_file_yields_empty_without_throw()
    {
        var path = WriteAuthFile("kaputt.json", "{not json");

        Assert.Empty(PiAuthDiscovery.DetectFile(path));
    }

    [Fact]
    public void Non_object_root_yields_empty_without_throw()
    {
        var path = WriteAuthFile("liste.json", """[1, 2, 3]""");

        Assert.Empty(PiAuthDiscovery.DetectFile(path));
    }

    private string WriteAuthFile(string name, string json)
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, name);
        File.WriteAllText(path, json);
        return path;
    }
}
