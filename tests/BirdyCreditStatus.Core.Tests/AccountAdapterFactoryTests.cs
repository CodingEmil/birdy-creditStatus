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

    private string WriteFile(string name, string content)
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, name);
        File.WriteAllText(path, content);
        return path;
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
