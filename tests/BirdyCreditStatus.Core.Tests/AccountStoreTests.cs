using BirdyCreditStatus.Core;

namespace BirdyCreditStatus.Core.Tests;

public sealed class AccountStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
    private readonly string _defaultAuthPath;
    private readonly string _missingClaudePath;
    private readonly string _missingGoPath;

    public AccountStoreTests()
    {
        _defaultAuthPath = Path.Combine(_directory, "default-auth.json");
        // Hermetisch: echte Profil-Defaults (Claude/Go) dürfen Tests nie beeinflussen.
        _missingClaudePath = Path.Combine(_directory, "fehlt-claude.json");
        _missingGoPath = Path.Combine(_directory, "fehlt-go.json");
    }

    private AccountStore HermeticStore() => new(_directory, _defaultAuthPath, _missingClaudePath, _missingGoPath);

    [Fact]
    public void Missing_file_without_default_auth_returns_empty()
    {
        var store = HermeticStore();

        Assert.Empty(store.Load());
    }

    [Fact]
    public void Add_persists_entry_and_Load_returns_it()
    {
        var store = HermeticStore();
        var authPath = WriteAuthFile("a.json", """{"tokens":{"access_token":"a","refresh_token":"r"}}""");

        Assert.True(store.Add(new Account(AccountProviders.Codex, "privat", authPath)));

        var loaded = store.Load();
        Assert.Single(loaded);
        Assert.Equal("privat", loaded[0].Name);
        Assert.Equal(authPath, loaded[0].AuthFilePath);
    }

    [Fact]
    public void Add_duplicate_name_returns_false_and_keeps_single_entry()
    {
        var store = HermeticStore();
        var authPath = WriteAuthFile("a.json", "{}");

        Assert.True(store.Add(new Account(AccountProviders.Codex, "privat", authPath)));
        Assert.False(store.Add(new Account(AccountProviders.Codex, "privat", authPath)));

        Assert.Single(store.Load());
    }

    [Fact]
    public void Rename_changes_name_and_keeps_auth_path()
    {
        var store = HermeticStore();
        var authPath = WriteAuthFile("a.json", "{}");
        store.Add(new Account(AccountProviders.Codex, "alt", authPath));

        Assert.True(store.Rename("alt", "neu"));

        var loaded = store.Load();
        Assert.Single(loaded);
        Assert.Equal("neu", loaded[0].Name);
        Assert.Equal(authPath, loaded[0].AuthFilePath);
    }

    [Fact]
    public void Rename_missing_or_duplicate_returns_false()
    {
        var store = HermeticStore();
        var authPath = WriteAuthFile("a.json", "{}");
        store.Add(new Account(AccountProviders.Codex, "a", authPath));
        store.Add(new Account(AccountProviders.Codex, "b", authPath));

        Assert.False(store.Rename("fehlt", "neu"));
        Assert.False(store.Rename("a", "b"));
        Assert.False(store.Rename("a", "  "));
        Assert.Equal(2, store.Load().Count);
    }

    [Fact]
    public void Remove_deletes_entry()
    {
        var store = HermeticStore();
        var authPath = WriteAuthFile("a.json", "{}");
        store.Add(new Account(AccountProviders.Codex, "a", authPath));
        store.Add(new Account(AccountProviders.Codex, "b", authPath));

        Assert.True(store.Remove("a"));
        Assert.False(store.Remove("a"));

        var loaded = store.Load();
        Assert.Single(loaded);
        Assert.Equal("b", loaded[0].Name);
    }

    [Fact]
    public void Corrupt_file_returns_empty_and_never_throws()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(Path.Combine(_directory, "accounts.json"), "{not json");
        var store = HermeticStore();

        Assert.Empty(store.Load());
        // Mutationen auf korruptem Stand starten sauber neu statt zu werfen.
        Assert.True(store.Add(new Account(AccountProviders.Codex, "neu", WriteAuthFile("a.json", "{}"))));
        Assert.Single(store.Load());
    }

    [Fact]
    public void Missing_file_with_default_auth_migrates_single_Codex_entry()
    {
        var authPath = WriteAuthFile("auth.json", """{"tokens":{"access_token":"a","refresh_token":"r"}}""");
        var store = new AccountStore(_directory, authPath, _missingClaudePath, _missingGoPath);

        var loaded = store.Load();

        Assert.Single(loaded);
        Assert.Equal("Codex", loaded[0].Name);
        Assert.Equal(authPath, loaded[0].AuthFilePath);
    }

    [Fact]
    public void Save_is_idempotent()
    {
        var store = HermeticStore();
        var authPath = WriteAuthFile("a.json", "{}");
        store.Add(new Account(AccountProviders.Codex, "a", authPath));

        var file = Path.Combine(_directory, "accounts.json");
        var first = File.ReadAllText(file);
        store.Save(store.Load());
        var second = File.ReadAllText(file);

        Assert.Equal(first, second);
    }

    [Fact]
    public void InMemory_fake_supports_add_rename_remove()
    {
        IAccountStore store = new InMemoryAccountStore();

        Assert.Empty(store.Load());
        Assert.True(store.Add(new Account(AccountProviders.Codex, "a", "C:\\a.json")));
        Assert.True(store.Add(new Account(AccountProviders.Codex, "b", "C:\\b.json")));
        Assert.False(store.Add(new Account(AccountProviders.Codex, "a", "C:\\a.json")));
        Assert.True(store.Rename("a", "c"));
        Assert.True(store.Remove("b"));

        var loaded = store.Load();
        Assert.Single(loaded);
        Assert.Equal("c", loaded[0].Name);
    }

    [Fact]
    public void Legacy_entries_without_provider_load_as_codex()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(
            Path.Combine(_directory, "accounts.json"),
            """[{"Name":"alt","AuthFilePath":"C:\\a.json"}]""");
        var store = HermeticStore();

        var loaded = store.Load();

        Assert.Single(loaded);
        Assert.Equal(AccountProviders.Codex, loaded[0].Provider);
        Assert.Equal("alt", loaded[0].Name);
    }

    [Fact]
    public void Unknown_provider_entries_are_dropped()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(
            Path.Combine(_directory, "accounts.json"),
            """[{"Provider":"fremd","Name":"x","AuthFilePath":"C:\\a.json"},{"Provider":"codex","Name":"ok","AuthFilePath":"C:\\b.json"}]""");
        var store = HermeticStore();

        var loaded = store.Load();

        Assert.Single(loaded);
        Assert.Equal("ok", loaded[0].Name);
    }

    [Fact]
    public void Missing_file_migrates_all_existing_defaults_in_block_order()
    {
        var codexAuth = WriteAuthFile("codex-auth.json", """{"tokens":{"access_token":"a","refresh_token":"r"}}""");
        var claudeCreds = WriteAuthFile("creds.json", """{"claudeAiOauth":{"accessToken":"a","refreshToken":"r","expiresAt":99}}""");
        var piAuth = WriteAuthFile("auth.json", """{"opencode-go":{"type":"api_key","key":"go-1"}}""");
        var store = new AccountStore(_directory, codexAuth, claudeCreds, piAuth);

        var loaded = store.Load();

        Assert.Equal(3, loaded.Count);
        Assert.Equal((AccountProviders.Claude, "Claude"), (loaded[0].Provider, loaded[0].Name));
        Assert.Equal((AccountProviders.Codex, "Codex"), (loaded[1].Provider, loaded[1].Name));
        Assert.Equal((AccountProviders.OpenCodeGo, "OpenCode Go"), (loaded[2].Provider, loaded[2].Name));
    }

    [Fact]
    public void Missing_go_key_skips_opencode_migration()
    {
        var piAuth = WriteAuthFile("auth.json", """{"anthropic":{"type":"oauth"}}""");
        var store = new AccountStore(
            _directory, Path.Combine(_directory, "fehlt-codex.json"),
            Path.Combine(_directory, "fehlt-claude.json"), piAuth);

        Assert.Empty(store.Load());
    }

    [Fact]
    public void Duplicate_name_across_providers_is_rejected()
    {
        var store = HermeticStore();

        Assert.True(store.Add(new Account(AccountProviders.Codex, "x", "C:\\a.json")));
        Assert.False(store.Add(new Account(AccountProviders.Claude, "x", "C:\\b.json")));

        Assert.Single(store.Load());
    }

    private string WriteAuthFile(string name, string json)
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, name);
        File.WriteAllText(path, json);
        return path;
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
