using BirdyCreditStatus.Core;

namespace BirdyCreditStatus.Core.Tests;

public sealed class FileOpenCodeGoCredentialStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

    [Fact]
    public async Task Reads_only_opencode_go_key_and_ignores_foreign_fields()
    {
        var path = WriteAuth(
            """{"anthropic":{"type":"oauth"},"opencode-go":{"type":"api_key","key":"go-123"},"openai-codex":{}}""");

        var key = await new FileOpenCodeGoCredentialStore(path).LoadAsync();

        Assert.Equal("go-123", key);
    }

    [Fact]
    public async Task Missing_file_returns_null()
    {
        var store = new FileOpenCodeGoCredentialStore(Path.Combine(_directory, "absent.json"));

        Assert.Null(await store.LoadAsync());
    }

    [Fact]
    public async Task Missing_field_returns_null()
    {
        var path = WriteAuth("""{"anthropic":{"type":"oauth"},"openai-codex":{}}""");

        Assert.Null(await new FileOpenCodeGoCredentialStore(path).LoadAsync());
    }

    [Fact]
    public async Task Corrupt_file_returns_null()
    {
        var path = WriteAuth("{not json");

        Assert.Null(await new FileOpenCodeGoCredentialStore(path).LoadAsync());
    }

    [Fact]
    public async Task Empty_key_returns_null()
    {
        var path = WriteAuth("""{"opencode-go":{"type":"api_key","key":""}}""");

        Assert.Null(await new FileOpenCodeGoCredentialStore(path).LoadAsync());
    }

    [Fact]
    public async Task Non_string_key_returns_null()
    {
        var path = WriteAuth("""{"opencode-go":{"type":"api_key","key":42}}""");

        Assert.Null(await new FileOpenCodeGoCredentialStore(path).LoadAsync());
    }

    private string WriteAuth(string json)
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
