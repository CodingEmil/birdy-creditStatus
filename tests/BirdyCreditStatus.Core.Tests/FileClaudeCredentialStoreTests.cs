using BirdyCreditStatus.Core;

namespace BirdyCreditStatus.Core.Tests;

public sealed class FileClaudeCredentialStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

    [Fact]
    public async Task Roundtrip_preserves_tokens_and_expiry()
    {
        var path = WriteCredentials("""{"claudeAiOauth":{"accessToken":"a1","refreshToken":"r1","expiresAt":123,"scopes":[]}}""");
        var store = new FileClaudeCredentialStore(path);

        var loaded = await store.LoadAsync();

        Assert.NotNull(loaded);
        Assert.Equal("a1", loaded.AccessToken);
        Assert.Equal("r1", loaded.RefreshToken);
        Assert.Equal(123, loaded.ExpiresAtUnixMs);

        await store.SaveAsync(new ClaudeOAuthCredentials("a2", "r2", 456));
        var reloaded = await store.LoadAsync();

        Assert.NotNull(reloaded);
        Assert.Equal("a2", reloaded.AccessToken);
        Assert.Equal("r2", reloaded.RefreshToken);
        Assert.Equal(456, reloaded.ExpiresAtUnixMs);
    }

    [Fact]
    public async Task Missing_file_returns_null()
    {
        var store = new FileClaudeCredentialStore(Path.Combine(_directory, "absent.json"));

        Assert.Null(await store.LoadAsync());
    }

    private string WriteCredentials(string json)
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, ".credentials.json");
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
