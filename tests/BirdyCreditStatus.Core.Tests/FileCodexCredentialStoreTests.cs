using BirdyCreditStatus.Core;

namespace BirdyCreditStatus.Core.Tests;

public sealed class FileCodexCredentialStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

    [Fact]
    public async Task Roundtrip_preserves_tokens_and_account()
    {
        var path = WriteCredentials(
            """{"auth_mode":"chatgpt","OPENAI_API_KEY":null,"tokens":{"access_token":"a1","refresh_token":"r1","account_id":"acc-1"},"last_refresh":"2026-09-19T00:00:00Z"}""");
        var store = new FileCodexCredentialStore(path);

        var loaded = await store.LoadAsync();

        Assert.NotNull(loaded);
        Assert.Equal("a1", loaded.AccessToken);
        Assert.Equal("r1", loaded.RefreshToken);
        Assert.Equal("acc-1", loaded.AccountId);

        await store.SaveAsync(new CodexOAuthCredentials("a2", "r2", "acc-1"));
        var reloaded = await store.LoadAsync();

        Assert.NotNull(reloaded);
        Assert.Equal("a2", reloaded.AccessToken);
        Assert.Equal("r2", reloaded.RefreshToken);
        Assert.Equal("acc-1", reloaded.AccountId);
    }

    [Fact]
    public async Task Missing_account_id_loads_without_it()
    {
        var path = WriteCredentials(
            """{"tokens":{"access_token":"a1","refresh_token":"r1"}}""");

        var loaded = await new FileCodexCredentialStore(path).LoadAsync();

        Assert.NotNull(loaded);
        Assert.Null(loaded.AccountId);
    }

    [Fact]
    public async Task Missing_file_returns_null()
    {
        var store = new FileCodexCredentialStore(Path.Combine(_directory, "absent.json"));

        Assert.Null(await store.LoadAsync());
    }

    [Fact]
    public async Task Corrupt_file_returns_null()
    {
        var path = WriteCredentials("{not json");

        Assert.Null(await new FileCodexCredentialStore(path).LoadAsync());
    }

    [Fact]
    public async Task Missing_tokens_return_null()
    {
        var path = WriteCredentials("""{"auth_mode":"chatgpt","OPENAI_API_KEY":"sk-x"}""");

        Assert.Null(await new FileCodexCredentialStore(path).LoadAsync());
    }

    private string WriteCredentials(string json)
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, "auth.json");
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
