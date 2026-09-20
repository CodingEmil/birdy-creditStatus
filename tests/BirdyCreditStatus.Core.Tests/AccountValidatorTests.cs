using BirdyCreditStatus.Core;

namespace BirdyCreditStatus.Core.Tests;

/// <summary>Naht (F006-T3): Dialog-Validierung — Name (nicht leer/eindeutig/nicht
/// reserviert) und Auth-Datei (lesbar mit Tokens). Meldungen deutsch, nie Throw.</summary>
public sealed class AccountValidatorTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

    [Fact]
    public void Valid_name_passes()
    {
        Assert.Null(AccountValidator.ValidateName("privat", ["Codex"]));
    }

    [Fact]
    public void Blank_name_fails()
    {
        Assert.NotNull(AccountValidator.ValidateName("", []));
        Assert.NotNull(AccountValidator.ValidateName("   ", []));
    }

    [Fact]
    public void Duplicate_name_fails()
    {
        Assert.NotNull(AccountValidator.ValidateName("privat", ["privat", "Arbeit"]));
    }

    [Fact]
    public void Formerly_reserved_provider_names_are_now_allowed()
    {
        Assert.Null(AccountValidator.ValidateName("Claude", []));
        Assert.Null(AccountValidator.ValidateName("OpenCode Go", []));
        Assert.Null(AccountValidator.ValidateName("Codex", []));
    }

    [Fact]
    public void Duplicate_name_across_providers_fails()
    {
        Assert.NotNull(AccountValidator.ValidateName("x", ["x"]));
    }

    [Fact]
    public void Migrated_single_name_stays_allowed()
    {
        Assert.Null(AccountValidator.ValidateName("Codex", ["privat"]));
    }

    [Fact]
    public void Missing_auth_file_fails()
    {
        Assert.NotNull(AccountValidator.ValidateAuthFile(Path.Combine(_directory, "fehlt.json")));
    }

    [Fact]
    public void Auth_file_with_tokens_passes()
    {
        var path = WriteFile("gut.json", """{"tokens":{"access_token":"a","refresh_token":"r"}}""");

        Assert.Null(AccountValidator.ValidateAuthFile(path));
    }

    [Fact]
    public void Auth_file_without_tokens_fails()
    {
        var broken = WriteFile("kaputt.json", """{"nope":true}""");
        var apiKeyOnly = WriteFile("apikey.json", """{"tokens":{"api_key":"sk-x"}}""");
        var empty = WriteFile("leer.json", "{not json");

        Assert.NotNull(AccountValidator.ValidateAuthFile(broken));
        Assert.NotNull(AccountValidator.ValidateAuthFile(apiKeyOnly));
        Assert.NotNull(AccountValidator.ValidateAuthFile(empty));
    }

    [Fact]
    public void Claude_auth_file_with_oauth_tokens_passes()
    {
        var path = WriteFile("claude.json", """{"claudeAiOauth":{"accessToken":"a","refreshToken":"r","expiresAt":99}}""");

        Assert.Null(AccountValidator.ValidateAuthFile(AccountProviders.Claude, path));
    }

    [Fact]
    public void Claude_auth_file_without_oauth_tokens_fails()
    {
        var codexFormat = WriteFile("falsch.json", """{"tokens":{"access_token":"a","refresh_token":"r"}}""");

        Assert.NotNull(AccountValidator.ValidateAuthFile(AccountProviders.Claude, codexFormat));
    }

    [Fact]
    public void OpenCodeGo_auth_file_with_key_passes()
    {
        var path = WriteFile("go.json", """{"opencode-go":{"type":"api_key","key":"go-1"},"fremd":1}""");

        Assert.Null(AccountValidator.ValidateAuthFile(AccountProviders.OpenCodeGo, path));
    }

    [Fact]
    public void OpenCodeGo_auth_file_without_key_fails()
    {
        var missing = WriteFile("ohne.json", """{"anthropic":{"type":"oauth"}}""");
        var emptyKey = WriteFile("leer-key.json", """{"opencode-go":{"key":""}}""");

        Assert.NotNull(AccountValidator.ValidateAuthFile(AccountProviders.OpenCodeGo, missing));
        Assert.NotNull(AccountValidator.ValidateAuthFile(AccountProviders.OpenCodeGo, emptyKey));
    }

    [Fact]
    public void Unknown_provider_fails_validation()
    {
        var path = WriteFile("gut.json", """{"tokens":{"access_token":"a","refresh_token":"r"}}""");

        Assert.NotNull(AccountValidator.ValidateAuthFile("fremd", path));
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
