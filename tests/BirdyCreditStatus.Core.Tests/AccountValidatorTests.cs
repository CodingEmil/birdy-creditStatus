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

    [Theory]
    [InlineData("123")]
    [InlineData("true")]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("null")]
    public void Wrong_token_field_types_return_validation_errors(string invalid)
    {
        foreach (var field in new[] { "access_token", "refresh_token" })
        {
            var tokens = new System.Text.Json.Nodes.JsonObject
            {
                ["access_token"] = "synthetic-access",
                ["refresh_token"] = "synthetic-refresh",
            };
            tokens[field] = System.Text.Json.Nodes.JsonNode.Parse(invalid);
            var path = WriteFile("codex-invalid.json", "{\"tokens\":" + tokens.ToJsonString() + "}");
            Assert.NotNull(AccountValidator.ValidateAuthFile(AccountProviders.Codex, path));
        }

        foreach (var field in new[] { "accessToken", "refreshToken" })
        {
            var tokens = new System.Text.Json.Nodes.JsonObject
            {
                ["accessToken"] = "synthetic-access",
                ["refreshToken"] = "synthetic-refresh",
                ["expiresAt"] = 99,
            };
            tokens[field] = System.Text.Json.Nodes.JsonNode.Parse(invalid);
            var path = WriteFile("claude-invalid.json", "{\"claudeAiOauth\":" + tokens.ToJsonString() + "}");
            Assert.NotNull(AccountValidator.ValidateAuthFile(AccountProviders.Claude, path));
        }
    }

    [Fact]
    public void Null_section_behaves_exactly_like_native_validation()
    {
        var valid = WriteFile("gut.json", """{"tokens":{"access_token":"a","refresh_token":"r"}}""");
        var invalid = WriteFile("falsch.json", """{"nope":true}""");

        Assert.Equal(
            AccountValidator.ValidateAuthFile(AccountProviders.Codex, valid),
            AccountValidator.ValidateAuthFile(AccountProviders.Codex, valid, null));
        Assert.Equal(
            AccountValidator.ValidateAuthFile(AccountProviders.Codex, invalid),
            AccountValidator.ValidateAuthFile(AccountProviders.Codex, invalid, null));
        Assert.Null(AccountValidator.ValidateAuthFile(AccountProviders.Codex, valid, null));
        Assert.NotNull(AccountValidator.ValidateAuthFile(AccountProviders.Codex, invalid, null));
    }

    [Fact]
    public void Pi_claude_section_with_access_refresh_expires_passes()
    {
        var path = WriteFile("pi.json", """
            {"anthropic": {"access": "a", "refresh": "r", "expires": 99},
             "openrouter": {"key": "x"}}
            """);

        Assert.Null(AccountValidator.ValidateAuthFile(AccountProviders.Claude, path, "anthropic"));
    }

    [Theory]
    [InlineData("""{"anthropic": {"refresh": "r", "expires": 99}}""")] // access fehlt
    [InlineData("""{"anthropic": {"access": "", "refresh": "r", "expires": 99}}""")] // access leer
    [InlineData("""{"anthropic": {"access": "a", "refresh": "r"}}""")] // expires fehlt
    [InlineData("""{"anthropic": {"access": "a", "refresh": "r", "expires": "bald"}}""")] // expires kein Zeitstempel
    [InlineData("""{"anthropic": "tot"}""")] // Section kein Objekt
    [InlineData("""{"openai-codex": {"access": "a", "refresh": "r", "accountId": "id-1"}}""")] // falsche Section
    public void Pi_claude_section_without_shape_fails_with_german_message(string json)
    {
        var path = WriteFile("pi.json", json);
        var section = json.Contains("openai-codex") ? "openai-codex" : "anthropic";

        var message = AccountValidator.ValidateAuthFile(AccountProviders.Claude, path, section);

        Assert.NotNull(message);
        Assert.Contains("Pi-Section", message);
    }

    [Fact]
    public void Pi_codex_sections_are_validated_individually_per_suffix()
    {
        var path = WriteFile("pi.json", """
            {"openai-codex": {"access": "a", "refresh": "r", "accountId": "id-1"},
             "openai-codex-2": {"access": "a2", "refresh": "r2", "accountId": "id-2"}}
            """);

        Assert.Null(AccountValidator.ValidateAuthFile(AccountProviders.Codex, path, "openai-codex"));
        Assert.Null(AccountValidator.ValidateAuthFile(AccountProviders.Codex, path, "openai-codex-2"));
    }

    [Theory]
    [InlineData("""{"openai-codex": {"refresh": "r", "accountId": "id-1"}}""")] // access fehlt
    [InlineData("""{"openai-codex": {"access": "a", "refresh": "", "accountId": "id-1"}}""")] // refresh leer
    [InlineData("""{"openai-codex": {"access": "a", "refresh": "r"}}""")] // accountId fehlt
    [InlineData("""{"openai-codex": {"access": "a", "refresh": "r", "accountId": 42}}""")] // accountId kein Text
    [InlineData("""{"anthropic": {"access": "a", "refresh": "r", "expires": 99}}""")] // fremdes Provider-Format
    public void Pi_codex_section_without_shape_fails_with_german_message(string json)
    {
        var path = WriteFile("pi.json", json);
        var section = json.Contains("anthropic") ? "anthropic" : "openai-codex";

        var message = AccountValidator.ValidateAuthFile(AccountProviders.Codex, path, section);

        Assert.NotNull(message);
        Assert.Contains("Pi-Section", message);
    }

    [Fact]
    public void Pi_go_section_with_key_passes_without_key_it_fails()
    {
        var valid = WriteFile("pi-gut.json", """{"opencode-go": {"key": "go-1"}}""");
        var empty = WriteFile("pi-leer.json", """{"opencode-go": {"key": ""}}""");
        var missing = WriteFile("pi-fremd.json", """{"anthropic": {"access": "a"}}""");

        Assert.Null(AccountValidator.ValidateAuthFile(AccountProviders.OpenCodeGo, valid, "opencode-go"));
        Assert.Contains("Pi-Section", AccountValidator.ValidateAuthFile(AccountProviders.OpenCodeGo, empty, "opencode-go")!);
        Assert.Contains("Pi-Section", AccountValidator.ValidateAuthFile(AccountProviders.OpenCodeGo, missing, "fremd")!);
    }

    [Fact]
    public void Pi_section_with_missing_or_corrupt_file_never_throws()
    {
        var corrupt = WriteFile("kaputt.json", "{not json");
        var missing = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName(), "fehlt.json");

        Assert.NotNull(AccountValidator.ValidateAuthFile(AccountProviders.Codex, missing, "openai-codex"));
        Assert.NotNull(AccountValidator.ValidateAuthFile(AccountProviders.Claude, corrupt, "anthropic"));
        Assert.NotNull(AccountValidator.ValidateAuthFile("fremd", corrupt, "anthropic"));
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
