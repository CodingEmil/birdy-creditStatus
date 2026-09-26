using System.Net;
using System.Text;
using BirdyCreditStatus.Core;

namespace BirdyCreditStatus.Core.Tests;

/// <summary>Naht (F007-T2): Konto → Adapter je Provider (shared HttpClient).
/// Unbekannter Provider fällt auf Codex zurück, nie Throw.</summary>
public sealed class AccountAdapterFactoryTests : IDisposable
{
    private const string CodexJson =
        """{"rate_limit":{"primary_window":{"used_percent":10},"secondary_window":{"used_percent":20}}}""";

    private const string ClaudeJson =
        """{"limits":[{"kind":"session","percent":2},{"kind":"weekly_all","percent":60}]}""";

    private const string GoJson =
        """{"usage":{"rolling":{"status":"ok","percent":30},"weekly":{"status":"ok","percent":45},"monthly":{"status":"ok","percent":12}}}""";

    private readonly string _directory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

    [Fact]
    public void Creates_codex_adapter_for_codex_accounts()
    {
        var adapter = AccountAdapterFactory.Create(
            new Account(AccountProviders.Codex, "Codex privat", "x.json"), new HttpClient());

        Assert.IsType<CodexQuotaAdapter>(adapter);
    }

    [Fact]
    public void Creates_claude_adapter_for_claude_accounts()
    {
        var adapter = AccountAdapterFactory.Create(
            new Account(AccountProviders.Claude, "Claude privat", "x.json"), new HttpClient());

        Assert.IsType<ClaudeQuotaAdapter>(adapter);
    }

    [Fact]
    public void Creates_go_adapter_for_go_accounts()
    {
        var adapter = AccountAdapterFactory.Create(
            new Account(AccountProviders.OpenCodeGo, "Go privat", "x.json"), new HttpClient());

        Assert.IsType<OpenCodeGoQuotaAdapter>(adapter);
    }

    [Fact]
    public void Unknown_provider_falls_back_to_codex()
    {
        var adapter = AccountAdapterFactory.Create(
            new Account("fremd", "X", "x.json"), new HttpClient());

        Assert.IsType<CodexQuotaAdapter>(adapter);
    }

    [Fact]
    public async Task Factory_adapters_fetch_with_account_name_as_provider()
    {
        var http = new HttpClient(new RoutingHandler());
        var codexAuth = WriteFile("codex.json", """{"tokens":{"access_token":"a","refresh_token":"r","account_id":"1"}}""");
        var claudeAuth = WriteFile("claude.json",
            "{\"claudeAiOauth\":{\"accessToken\":\"a\",\"refreshToken\":\"r\",\"expiresAt\":" + DateTimeOffset.UtcNow.AddYears(1).ToUnixTimeMilliseconds() + "}}");
        var goAuth = WriteFile("go.json", """{"opencode-go":{"key":"go-1"}}""");

        var codex = await AccountAdapterFactory
            .Create(new Account(AccountProviders.Codex, "Codex privat", codexAuth), http).FetchAsync();
        var claude = await AccountAdapterFactory
            .Create(new Account(AccountProviders.Claude, "Claude privat", claudeAuth), http).FetchAsync();
        var go = await AccountAdapterFactory
            .Create(new Account(AccountProviders.OpenCodeGo, "Go privat", goAuth), http).FetchAsync();

        Assert.True(codex.IsSuccess);
        Assert.Equal("Codex privat", codex.Snapshot!.Provider);
        Assert.Equal(["5 Stunden", "Woche"], codex.Snapshot.Windows.Select(w => w.Name));
        Assert.True(claude.IsSuccess);
        Assert.Equal("Claude privat", claude.Snapshot!.Provider);
        Assert.Equal(["Session", "Woche"], claude.Snapshot.Windows.Select(w => w.Name));
        Assert.True(go.IsSuccess);
        Assert.Equal("Go privat", go.Snapshot!.Provider);
        Assert.Equal(["Rolling", "Woche", "Monat"], go.Snapshot.Windows.Select(w => w.Name));
    }

    [Fact]
    public async Task Pi_accounts_fetch_per_section_with_existing_window_mapping()
    {
        var http = new HttpClient(new RoutingHandler());
        var expires = DateTimeOffset.UtcNow.AddYears(1).ToUnixTimeMilliseconds();
        var piAuth = WriteFile("pi-auth.json",
            "{\"anthropic\":{\"access\":\"a\",\"refresh\":\"r\",\"expires\":" + expires + "}," +
            "\"openai-codex\":{\"access\":\"a\",\"refresh\":\"r\",\"accountId\":\"id-1\"}," +
            "\"openai-codex-2\":{\"access\":\"a2\",\"refresh\":\"r2\",\"accountId\":\"id-2\"}," +
            "\"opencode-go\":{\"key\":\"go-1\"}}");
        var before = await File.ReadAllBytesAsync(piAuth);

        var claude = await AccountAdapterFactory
            .Create(new Account(AccountProviders.Claude, "Pi Claude", piAuth, "anthropic"), http).FetchAsync();
        var codex = await AccountAdapterFactory
            .Create(new Account(AccountProviders.Codex, "Pi Codex", piAuth, "openai-codex"), http).FetchAsync();
        var codex2 = await AccountAdapterFactory
            .Create(new Account(AccountProviders.Codex, "Pi Codex 2", piAuth, "openai-codex-2"), http).FetchAsync();
        var go = await AccountAdapterFactory
            .Create(new Account(AccountProviders.OpenCodeGo, "Pi Go", piAuth, "opencode-go"), http).FetchAsync();

        Assert.True(claude.IsSuccess);
        Assert.Equal("Pi Claude", claude.Snapshot!.Provider);
        Assert.Equal(["Session", "Woche"], claude.Snapshot.Windows.Select(w => w.Name));
        Assert.True(codex.IsSuccess);
        Assert.Equal("Pi Codex", codex.Snapshot!.Provider);
        Assert.Equal(["5 Stunden", "Woche"], codex.Snapshot.Windows.Select(w => w.Name));
        Assert.True(codex2.IsSuccess);
        Assert.Equal("Pi Codex 2", codex2.Snapshot!.Provider);
        Assert.True(go.IsSuccess);
        Assert.Equal("Pi Go", go.Snapshot!.Provider);
        Assert.Equal(["Rolling", "Woche", "Monat"], go.Snapshot.Windows.Select(w => w.Name));
        Assert.Equal(before, await File.ReadAllBytesAsync(piAuth));
    }

    [Fact]
    public async Task Dead_pi_tokens_yield_na_without_affecting_healthy_cards()
    {
        var handler = new DeadCodexHandler();
        var http = new HttpClient(handler);
        var expires = DateTimeOffset.UtcNow.AddYears(1).ToUnixTimeMilliseconds();
        var piAuth = WriteFile("pi-auth.json",
            "{\"anthropic\":{\"access\":\"a\",\"refresh\":\"r\",\"expires\":" + expires + "}," +
            "\"openai-codex\":{\"access\":\"tot\",\"refresh\":\"tot-r\",\"accountId\":\"id-1\"}}");
        var before = await File.ReadAllBytesAsync(piAuth);

        var healthy = await AccountAdapterFactory
            .Create(new Account(AccountProviders.Claude, "Pi Claude", piAuth, "anthropic"), http).FetchAsync();
        var dead = await AccountAdapterFactory
            .Create(new Account(AccountProviders.Codex, "Pi Codex", piAuth, "openai-codex"), http).FetchAsync();

        Assert.True(healthy.IsSuccess);
        Assert.False(dead.IsSuccess);
        Assert.Null(dead.Snapshot);
        Assert.Equal("n/a – Key prüfen / offline", dead.Error);
        Assert.Equal(before, await File.ReadAllBytesAsync(piAuth));
    }

    [Fact]
    public async Task Expired_pi_claude_rotates_in_memory_and_never_writes_the_file()
    {
        var http = new HttpClient(new RefreshingClaudeHandler());
        var piAuth = WriteFile("pi-auth.json", """{"anthropic": {"access": "alt", "refresh": "r", "expires": 1}}""");
        var before = await File.ReadAllBytesAsync(piAuth);

        var results = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => AccountAdapterFactory
            .Create(new Account(AccountProviders.Claude, "Pi Claude", piAuth, "anthropic"), http).FetchAsync()));

        Assert.All(results, r => Assert.True(r.IsSuccess));
        Assert.All(results, r => Assert.Equal(["Session", "Woche"], r.Snapshot!.Windows.Select(w => w.Name)));
        Assert.Equal(before, await File.ReadAllBytesAsync(piAuth));
    }

    [Fact]
    public async Task Pi_codex_401_with_valid_refresh_rotates_and_retries_without_writing()
    {
        var http = new HttpClient(new RefreshingCodexHandler());
        var piAuth = WriteFile("pi-auth.json",
            """{"openai-codex": {"access": "alt", "refresh": "r", "accountId": "id-1"}}""");
        var before = await File.ReadAllBytesAsync(piAuth);

        var result = await AccountAdapterFactory
            .Create(new Account(AccountProviders.Codex, "Pi Codex", piAuth, "openai-codex"), http).FetchAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(["5 Stunden", "Woche"], result.Snapshot!.Windows.Select(w => w.Name));
        Assert.Equal(before, await File.ReadAllBytesAsync(piAuth));
    }

    private string WriteFile(string name, string content)
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, name);
        File.WriteAllText(path, content);
        return path;
    }

    private sealed class DeadCodexHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var url = request.RequestUri?.ToString() ?? string.Empty;
            if (url.Contains("/wham/usage"))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized));
            }
            if (url.Contains("oauth/token"))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
                {
                    Content = new StringContent("""{"error":"invalid_grant"}""", Encoding.UTF8, "application/json"),
                });
            }
            var json = url.Contains("anthropic.com") ? ClaudeJson : GoJson;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            });
        }
    }

    private sealed class RefreshingClaudeHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var url = request.RequestUri?.ToString() ?? string.Empty;
            if (url.Contains("oauth/token"))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        """{"access_token":"frisch","expires_in":3600}""", Encoding.UTF8, "application/json"),
                });
            }
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(ClaudeJson, Encoding.UTF8, "application/json"),
            });
        }
    }

    private sealed class RefreshingCodexHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var url = request.RequestUri?.ToString() ?? string.Empty;
            if (url.Contains("oauth/token"))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        """{"access_token":"frisch","refresh_token":"neu-r"}""", Encoding.UTF8, "application/json"),
                });
            }
            var bearer = request.Headers.Authorization?.Parameter ?? string.Empty;
            if (!string.Equals(bearer, "frisch", StringComparison.Ordinal))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized));
            }
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(CodexJson, Encoding.UTF8, "application/json"),
            });
        }
    }

    private sealed class RoutingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var url = request.RequestUri?.ToString() ?? string.Empty;
            var json = url.Contains("/wham/usage") ? CodexJson
                : url.Contains("anthropic.com") ? ClaudeJson
                : GoJson;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            });
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
