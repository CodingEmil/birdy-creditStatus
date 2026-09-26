using BirdyCreditStatus.Core;

namespace BirdyCreditStatus.Core.Tests;

/// <summary>Naht (F010-T1): Default-Erkennung — nur die 3 Defaults, Format per Marker,
/// Go nur mit Key, Konfiguriertes raus, nie Throw.</summary>
public sealed class DefaultAuthDiscoveryTests : IDisposable
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
    public void DefaultCandidates_lists_exactly_the_three_provider_defaults()
    {
        var candidates = DefaultAuthDiscovery.DefaultCandidates();

        Assert.Equal(3, candidates.Count);
        Assert.Equal(
            [AccountProviders.Claude, AccountProviders.Codex, AccountProviders.OpenCodeGo],
            candidates.Select(c => c.Provider).Order());
        foreach (var (provider, path) in candidates)
        {
            Assert.Equal(AccountProviderPaths.DefaultAuthPath(provider), path);
        }
    }

    [Fact]
    public void Codex_shaped_file_is_detected_with_display_name_and_path()
    {
        var path = WriteAuthFile("codex.json", """{"tokens":{"access_token":"a","refresh_token":"r"}}""");

        var found = DefaultAuthDiscovery.DetectFrom([(AccountProviders.Codex, path)]);

        var single = Assert.Single(found);
        Assert.Equal(AccountProviders.Codex, single.Provider);
        Assert.Equal("Codex", single.DisplayName);
        Assert.Equal(path, single.Path);
    }

    [Fact]
    public void Claude_shaped_file_is_detected_as_claude()
    {
        var path = WriteAuthFile("claude.json",
            """{"claudeAiOauth":{"accessToken":"a","refreshToken":"r","expiresAt":123}}""");

        var found = DefaultAuthDiscovery.DetectFrom([(AccountProviders.Claude, path)]);

        var single = Assert.Single(found);
        Assert.Equal(AccountProviders.Claude, single.Provider);
        Assert.Equal("Claude", single.DisplayName);
    }

    [Fact]
    public void OpenCodeGo_file_with_key_is_detected_without_key_it_is_skipped()
    {
        var withKey = WriteAuthFile("go.json", """{"opencode-go":{"key":"k"}}""");
        var withoutKey = WriteAuthFile("go-leer.json", """{"opencode-go":{}}""");

        var found = DefaultAuthDiscovery.DetectFrom(
            [(AccountProviders.OpenCodeGo, withKey), (AccountProviders.OpenCodeGo, withoutKey)]);

        var single = Assert.Single(found);
        Assert.Equal(withKey, single.Path);
    }

    [Fact]
    public void Marker_mismatch_is_excluded()
    {
        var codexFile = WriteAuthFile("codex.json", """{"tokens":{"access_token":"a","refresh_token":"r"}}""");

        Assert.Empty(DefaultAuthDiscovery.DetectFrom([(AccountProviders.Claude, codexFile)]));
    }

    [Fact]
    public void Already_configured_paths_are_filtered_case_insensitively()
    {
        var path = WriteAuthFile("codex.json", """{"tokens":{"access_token":"a","refresh_token":"r"}}""");

        Assert.Empty(DefaultAuthDiscovery.DetectFrom(
            [(AccountProviders.Codex, path)], [path.ToUpperInvariant()]));
    }

    [Fact]
    public void Missing_files_yield_empty_without_throw()
    {
        var missing = Path.Combine(_directory, "fehlt.json");

        Assert.Empty(DefaultAuthDiscovery.DetectFrom([(AccountProviders.Codex, missing)]));
    }

    [Fact]
    public void Detect_never_throws_and_only_returns_existing_valid_files()
    {
        var found = DefaultAuthDiscovery.Detect([]);

        foreach (var candidate in found)
        {
            Assert.True(File.Exists(candidate.Path));
            Assert.Null(AccountValidator.ValidateAuthFile(candidate.Provider, candidate.Path));
        }
    }

    [Fact]
    public void Pi_hits_are_validated_and_listed_per_section()
    {
        var piAuth = WriteAuthFile("pi-auth.json", """
            {"anthropic": {"access": "a", "refresh": "r", "expires": 99},
             "openai-codex": {"access": "a", "refresh": "r", "accountId": "id-1"},
             "openai-codex-2": {"access": "a2", "refresh": "r2", "accountId": "id-2"},
             "opencode-go": {"key": "go-1"},
             "openrouter": {"key": "x"}}
            """);

        var found = DefaultAuthDiscovery.DetectAccountsFrom([], [], piAuth);

        Assert.Equal(
            ["anthropic", "openai-codex", "openai-codex-2", "opencode-go"],
            found.Select(c => c.Section));
        Assert.All(found, c => Assert.Equal(piAuth, c.Path));
    }

    [Fact]
    public void Invalid_pi_sections_are_excluded()
    {
        var piAuth = WriteAuthFile("pi-auth.json", """
            {"anthropic": {"access": "a", "refresh": "r"},
             "openai-codex": {"access": "a", "refresh": "r", "accountId": "id-1"}}
            """);

        var found = DefaultAuthDiscovery.DetectAccountsFrom([], [], piAuth);

        var single = Assert.Single(found);
        Assert.Equal("openai-codex", single.Section);
    }

    [Fact]
    public void Configured_section_pairs_are_filtered_but_siblings_remain()
    {
        var piAuth = WriteAuthFile("pi-auth.json", """
            {"anthropic": {"access": "a", "refresh": "r", "expires": 99},
             "openai-codex": {"access": "a", "refresh": "r", "accountId": "id-1"}}
            """);
        var accounts = new[]
        {
            new Account(AccountProviders.Codex, "Pi Codex", piAuth.ToUpperInvariant(), "openai-codex"),
        };

        var found = DefaultAuthDiscovery.DetectAccountsFrom(accounts, [], piAuth);

        var single = Assert.Single(found);
        Assert.Equal("anthropic", single.Section);
    }

    [Fact]
    public void Native_go_on_pi_path_covers_pi_go_section()
    {
        var piAuth = WriteAuthFile("pi-auth.json", """
            {"anthropic": {"access": "a", "refresh": "r", "expires": 99},
             "opencode-go": {"key": "go-1"}}
            """);
        var accounts = new[]
        {
            new Account(AccountProviders.OpenCodeGo, "OpenCode Go", piAuth, null),
        };

        var found = DefaultAuthDiscovery.DetectAccountsFrom(accounts, [], piAuth);

        var single = Assert.Single(found);
        Assert.Equal("anthropic", single.Section);
    }

    [Fact]
    public void Native_candidates_on_pi_path_are_superseded_by_pi_hits()
    {
        var piAuth = WriteAuthFile("pi-auth.json", """{"opencode-go": {"key": "go-1"}}""");
        var natives = new[] { (AccountProviders.OpenCodeGo, piAuth) };

        var found = DefaultAuthDiscovery.DetectAccountsFrom([], natives, piAuth);

        var single = Assert.Single(found);
        Assert.Equal("opencode-go", single.Section);
        Assert.Equal(AccountProviders.OpenCodeGo, single.Provider);
    }

    [Fact]
    public void ExcludeConfiguredSections_removes_configured_pairs_only()
    {
        var hits = new[]
        {
            new DetectedAuth(AccountProviders.Claude, "Claude", "C:\\pi\\auth.json", "anthropic"),
            new DetectedAuth(AccountProviders.Codex, "Codex", "C:\\pi\\auth.json", "openai-codex"),
            new DetectedAuth(AccountProviders.Codex, "Codex", "C:\\nativ\\codex.json", null),
        };
        var accounts = new[]
        {
            new Account(AccountProviders.Codex, "Pi Codex", "c:\\PI\\auth.json", "openai-codex"),
        };

        var remaining = DefaultAuthDiscovery.ExcludeConfiguredSections(hits, accounts);

        Assert.Equal(2, remaining.Count);
        Assert.DoesNotContain(remaining, c => c.Section == "openai-codex");
        Assert.Empty(DefaultAuthDiscovery.ExcludeConfiguredSections(null, null));
    }

    [Fact]
    public void Missing_pi_file_yields_native_only()
    {
        var native = WriteAuthFile("codex.json", """{"tokens":{"access_token":"a","refresh_token":"r"}}""");
        var natives = new[] { (AccountProviders.Codex, native) };

        var found = DefaultAuthDiscovery.DetectAccountsFrom(
            [], natives, Path.Combine(_directory, "fehlt.json"));

        var single = Assert.Single(found);
        Assert.Null(single.Section);
        Assert.Equal(native, single.Path);
    }

    private string WriteAuthFile(string name, string json)
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, name);
        File.WriteAllText(path, json);
        return path;
    }
}
