using BirdyCreditStatus.Core;

namespace BirdyCreditStatus.Core.Tests;

/// <summary>Naht (F011-T3): Pi-Credential-Stores — Section-Mapping je Anbieter,
/// SaveAsync-No-Op (Pi-Datei wird nie beschrieben), nie Throw.</summary>
public sealed class PiCredentialStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

    [Fact]
    public async Task PiClaude_maps_access_refresh_expires_from_named_section()
    {
        var path = WriteFile("pi-auth.json", """
            {"anthropic": {"access": "a1", "refresh": "r1", "expires": 1234567890123},
             "openai-codex": {"access": "x", "refresh": "y", "accountId": "id-1"}}
            """);

        var loaded = await new PiClaudeCredentialStore(path, "anthropic").LoadAsync();

        Assert.NotNull(loaded);
        Assert.Equal("a1", loaded.AccessToken);
        Assert.Equal("r1", loaded.RefreshToken);
        Assert.Equal(1234567890123, loaded.ExpiresAtUnixMs);
    }

    [Theory]
    [InlineData("""{"openai-codex": {"access": "x", "refresh": "y", "accountId": "id-1"}}""")] // Section fehlt
    [InlineData("""{"anthropic": {"refresh": "r", "expires": 99}}""")] // access fehlt
    [InlineData("""{"anthropic": {"access": "", "refresh": "r", "expires": 99}}""")] // access leer
    [InlineData("""{"anthropic": {"access": "a", "refresh": "r"}}""")] // expires fehlt
    [InlineData("""{"anthropic": "tot"}""")] // Section kein Objekt
    [InlineData("{not json")] // korrupt
    public async Task PiClaude_without_shape_returns_null(string json)
    {
        var path = WriteFile("pi-auth.json", json);

        Assert.Null(await new PiClaudeCredentialStore(path, "anthropic").LoadAsync());
    }

    [Fact]
    public async Task PiClaude_missing_file_or_section_returns_null()
    {
        Assert.Null(await new PiClaudeCredentialStore(Path.Combine(_directory, "fehlt.json"), "anthropic").LoadAsync());

        var path = WriteFile("pi-auth.json", """{"anthropic": {"access": "a", "refresh": "r", "expires": 99}}""");
        Assert.Null(await new PiClaudeCredentialStore(path, "openai-codex").LoadAsync());
        Assert.Null(await new PiClaudeCredentialStore(path, "").LoadAsync());
        Assert.Null(await new PiClaudeCredentialStore(null, "anthropic").LoadAsync());
    }

    [Fact]
    public async Task PiClaude_save_is_noop_and_never_writes_the_file()
    {
        var path = WriteFile("pi-auth.json", """{"anthropic": {"access": "a", "refresh": "r", "expires": 99}}""");
        var before = await File.ReadAllBytesAsync(path);
        var store = new PiClaudeCredentialStore(path, "anthropic");

        await store.SaveAsync(new ClaudeOAuthCredentials("neu", "neu-r", 1));
        await store.SaveAsync(new ClaudeOAuthCredentials("neu2", "neu-r2", 2));

        Assert.Equal(before, await File.ReadAllBytesAsync(path));
        var reloaded = await store.LoadAsync();
        Assert.Equal("a", reloaded!.AccessToken);
    }

    [Fact]
    public async Task PiCodex_maps_access_refresh_and_optional_accountId()
    {
        var path = WriteFile("pi-auth.json", """
            {"openai-codex": {"access": "a1", "refresh": "r1", "accountId": "id-1"},
             "openai-codex-2": {"access": "a2", "refresh": "r2"}}
            """);

        var first = await new PiCodexCredentialStore(path, "openai-codex").LoadAsync();
        Assert.NotNull(first);
        Assert.Equal(("a1", "r1", "id-1"), (first.AccessToken, first.RefreshToken, first.AccountId));

        var second = await new PiCodexCredentialStore(path, "openai-codex-2").LoadAsync();
        Assert.NotNull(second);
        Assert.Equal("a2", second.AccessToken);
        Assert.Null(second.AccountId);
    }

    [Theory]
    [InlineData("""{"anthropic": {"access": "a", "refresh": "r", "expires": 99}}""")] // Section fehlt
    [InlineData("""{"openai-codex": {"refresh": "r", "accountId": "id-1"}}""")] // access fehlt
    [InlineData("""{"openai-codex": {"access": "a", "refresh": "", "accountId": "id-1"}}""")] // refresh leer
    [InlineData("[1,2]")] // kein Objekt
    public async Task PiCodex_without_shape_returns_null(string json)
    {
        var path = WriteFile("pi-auth.json", json);

        Assert.Null(await new PiCodexCredentialStore(path, "openai-codex").LoadAsync());
    }

    [Fact]
    public async Task PiCodex_save_is_noop_and_never_writes_the_file()
    {
        var path = WriteFile("pi-auth.json", """{"openai-codex": {"access": "a", "refresh": "r", "accountId": "id-1"}}""");
        var before = await File.ReadAllBytesAsync(path);
        var store = new PiCodexCredentialStore(path, "openai-codex");

        await store.SaveAsync(new CodexOAuthCredentials("neu", "neu-r", "neu-id"));

        Assert.Equal(before, await File.ReadAllBytesAsync(path));
    }

    private string WriteFile(string name, string content)
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, name);
        File.WriteAllText(path, content);
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
