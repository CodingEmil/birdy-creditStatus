using System.Net;
using System.Text;
using BirdyCreditStatus.Core;

namespace BirdyCreditStatus.Core.Tests;

public sealed class CodexQuotaAdapterTests
{
    private const string UsageJson =
        """{"plan_type":"plus","rate_limit":{"primary_window":{"used_percent":0,"limit_window_seconds":18000,"reset_after_seconds":16446,"reset_at":1789860748},"secondary_window":{"used_percent":31,"limit_window_seconds":604800,"reset_after_seconds":576187,"reset_at":1790420489}},"additional_rate_limits":null}""";

    [Fact]
    public async Task Usage_windows_map_to_five_hour_and_week()
    {
        var adapter = new CodexQuotaAdapter(
            StubHttp(UsageJson),
            new StubStore(new CodexOAuthCredentials("access", "refresh", "acc-1")));

        var result = await adapter.FetchAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal("Codex", result.Snapshot!.Provider);
        Assert.Equal(["5 Stunden", "Woche"], result.Snapshot.Windows.Select(w => w.Name));
        Assert.Equal([100.0, 69.0], result.Snapshot.Windows.Select(w => w.PercentRemaining));
        Assert.Equal(
            DateTimeOffset.FromUnixTimeSeconds(1789860748),
            result.Snapshot.Windows[0].ResetsAt);
        Assert.Equal(
            DateTimeOffset.FromUnixTimeSeconds(1790420489),
            result.Snapshot.Windows[1].ResetsAt);
    }

    [Fact]
    public async Task Bucket_rule_prefers_codex_over_additional()
    {
        var adapter = new CodexQuotaAdapter(
            StubHttp("""{"rate_limit":{"primary_window":{"used_percent":10}},"additional_rate_limits":[{"limit_name":"codex_other","metered_feature":"codex_other","rate_limit":{"primary_window":{"used_percent":88}}}]}"""),
            new StubStore(new CodexOAuthCredentials("access", "refresh", "acc-1")));

        var result = await adapter.FetchAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal([90.0], result.Snapshot!.Windows.Select(w => w.PercentRemaining));
    }

    [Fact]
    public async Task Fallback_first_entry_without_primary()
    {
        var adapter = new CodexQuotaAdapter(
            StubHttp("""{"additional_rate_limits":[{"limit_name":"codex_other","metered_feature":"codex_other","rate_limit":{"primary_window":{"used_percent":20},"secondary_window":{"used_percent":40}}}]}"""),
            new StubStore(new CodexOAuthCredentials("access", "refresh", "acc-1")));

        var result = await adapter.FetchAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(["5 Stunden", "Woche"], result.Snapshot!.Windows.Select(w => w.Name));
        Assert.Equal([80.0, 60.0], result.Snapshot.Windows.Select(w => w.PercentRemaining));
    }

    [Fact]
    public async Task Single_window_without_reset_maps_to_week_without_resets_at()
    {
        var adapter = new CodexQuotaAdapter(
            StubHttp("""{"rate_limit":{"secondary_window":{"used_percent":31}}}"""),
            new StubStore(new CodexOAuthCredentials("access", "refresh", "acc-1")));

        var result = await adapter.FetchAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(["Woche"], result.Snapshot!.Windows.Select(w => w.Name));
        Assert.Null(result.Snapshot.Windows[0].ResetsAt);
    }

    [Fact]
    public async Task Empty_payload_maps_to_not_available()
    {
        var adapter = new CodexQuotaAdapter(
            StubHttp("""{"plan_type":"plus","rate_limit":null}"""),
            new StubStore(new CodexOAuthCredentials("access", "refresh", "acc-1")));

        var result = await adapter.FetchAsync();

        Assert.False(result.IsSuccess);
        Assert.Null(result.Snapshot);
        Assert.Equal("n/a – Key prüfen / offline", result.Error);
    }

    [Fact]
    public async Task Overuse_clamps_remaining_to_zero()
    {
        var adapter = new CodexQuotaAdapter(
            StubHttp("""{"rate_limit":{"primary_window":{"used_percent":130},"secondary_window":{"used_percent":100}}}"""),
            new StubStore(new CodexOAuthCredentials("access", "refresh", "acc-1")));

        var result = await adapter.FetchAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal([0.0, 0.0], result.Snapshot!.Windows.Select(w => w.PercentRemaining));
    }

    [Fact]
    public async Task Sends_bearer_and_account_headers_without_luna_reserve()
    {
        HttpRequestMessage? usageRequest = null;
        var handler = new SequenceHandler((request, _) =>
        {
            usageRequest = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(UsageJson, Encoding.UTF8, "application/json"),
            });
        });
        var adapter = new CodexQuotaAdapter(
            new HttpClient(handler),
            new StubStore(new CodexOAuthCredentials("access", "refresh", "acc-1")));

        await adapter.FetchAsync();

        Assert.NotNull(usageRequest);
        Assert.Equal("Bearer", usageRequest.Headers.Authorization?.Scheme);
        Assert.Equal("access", usageRequest.Headers.Authorization?.Parameter);
        Assert.True(usageRequest.Headers.TryGetValues("ChatGPT-Account-ID", out var account));
        Assert.Equal("acc-1", account.Single());
        Assert.False(usageRequest.Headers.Contains("x-openai-codex-luna-reserve"));
    }

    [Fact]
    public async Task Expired_token_refreshes_transparently_and_retries_usage()
    {
        HttpRequestMessage? refreshRequest = null;
        string? refreshBody = null;
        HttpRequestMessage? usageRequest = null;
        var store = new StubStore(new CodexOAuthCredentials("old-access", "refresh-123", "acc-1"));
        var usageCalls = 0;
        var handler = new SequenceHandler(async (request, _) =>
        {
            if (request.RequestUri!.AbsoluteUri.Contains("oauth/token"))
            {
                refreshRequest = request;
                refreshBody = await request.Content!.ReadAsStringAsync();
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        """{"access_token":"new-access"}""",
                        Encoding.UTF8, "application/json"),
                };
            }

            usageCalls++;
            usageRequest = request;
            if (usageCalls == 1)
            {
                return new HttpResponseMessage(HttpStatusCode.Unauthorized);
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(UsageJson, Encoding.UTF8, "application/json"),
            };
        });
        var adapter = new CodexQuotaAdapter(
            new HttpClient(handler), store, tokenEndpointOverride: "https://example.test/oauth/token");

        var result = await adapter.FetchAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal([100.0, 69.0], result.Snapshot!.Windows.Select(w => w.PercentRemaining));
        Assert.NotNull(refreshRequest);
        Assert.Contains("refresh_token", refreshBody);
        Assert.Contains(CodexQuotaAdapter.ClientId, refreshBody);
        Assert.Equal("new-access", store.Saved?.AccessToken);
        Assert.Equal("refresh-123", store.Saved?.RefreshToken);
        Assert.Equal("new-access", usageRequest!.Headers.Authorization?.Parameter);
    }

    [Fact]
    public async Task Refresh_rotates_refresh_token_when_present()
    {
        var store = new StubStore(new CodexOAuthCredentials("old-access", "refresh-123", "acc-1"));
        var handler = new SequenceHandler((request, _) =>
        {
            if (request.RequestUri!.AbsoluteUri.Contains("oauth/token"))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        """{"access_token":"new-access","refresh_token":"refresh-456"}""",
                        Encoding.UTF8, "application/json"),
                });
            }

            if (request.Headers.Authorization?.Parameter == "old-access")
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized));
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(UsageJson, Encoding.UTF8, "application/json"),
            });
        });
        var adapter = new CodexQuotaAdapter(
            new HttpClient(handler), store, tokenEndpointOverride: "https://example.test/oauth/token");

        var result = await adapter.FetchAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal("refresh-456", store.Saved?.RefreshToken);
    }

    [Fact]
    public async Task Dead_credentials_map_to_setup_state()
    {
        var handler = new SequenceHandler((request, _) =>
        {
            if (request.RequestUri!.AbsoluteUri.Contains("oauth/token"))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
                {
                    Content = new StringContent("""{"error":"invalid_grant"}""", Encoding.UTF8, "application/json"),
                });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized));
        });
        var adapter = new CodexQuotaAdapter(
            new HttpClient(handler),
            new StubStore(new CodexOAuthCredentials("old-access", "dead-refresh", "acc-1")),
            tokenEndpointOverride: "https://example.test/oauth/token");

        var result = await adapter.FetchAsync();

        Assert.False(result.IsSuccess);
        Assert.True(result.LoginRequired);
    }

    [Fact]
    public async Task Failed_refresh_maps_to_not_available()
    {
        var handler = new SequenceHandler((request, _) =>
        {
            if (request.RequestUri!.AbsoluteUri.Contains("oauth/token"))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized));
        });
        var adapter = new CodexQuotaAdapter(
            new HttpClient(handler),
            new StubStore(new CodexOAuthCredentials("old-access", "refresh-123", "acc-1")),
            tokenEndpointOverride: "https://example.test/oauth/token");

        var result = await adapter.FetchAsync();

        Assert.False(result.IsSuccess);
        Assert.False(result.LoginRequired);
        Assert.Equal("n/a – Key prüfen / offline", result.Error);
    }

    [Fact]
    public async Task Offline_and_timeout_map_to_not_available_without_throw()
    {
        var offline = new CodexQuotaAdapter(
            new HttpClient(new ThrowingHandler(new HttpRequestException("offline"))),
            new StubStore(new CodexOAuthCredentials("access", "refresh", "acc-1")));
        var timeout = new CodexQuotaAdapter(
            new HttpClient(new ThrowingHandler(new TaskCanceledException("timeout"))),
            new StubStore(new CodexOAuthCredentials("access", "refresh", "acc-1")));

        Assert.False((await offline.FetchAsync()).LoginRequired);
        Assert.Equal("n/a – Key prüfen / offline", (await offline.FetchAsync()).Error);
        Assert.False((await timeout.FetchAsync()).LoginRequired);
        Assert.Equal("n/a – Key prüfen / offline", (await timeout.FetchAsync()).Error);
    }

    [Fact]
    public async Task Missing_login_maps_to_setup_state()
    {
        var adapter = new CodexQuotaAdapter(StubHttp(UsageJson), new StubStore(null));

        var result = await adapter.FetchAsync();

        Assert.False(result.IsSuccess);
        Assert.Equal("n/a – Key prüfen / offline", result.Error);
        Assert.True(result.LoginRequired);
    }

    private static HttpClient StubHttp(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new(new StubHandler(new HttpResponseMessage(status)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        }));

    private sealed class SequenceHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            respond(request, cancellationToken);
    }

    private sealed class ThrowingHandler(Exception error) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromException<HttpResponseMessage>(error);
    }

    private sealed class StubHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(response);
    }

    private sealed class StubStore(CodexOAuthCredentials? credentials) : ICodexCredentialStore
    {
        public CodexOAuthCredentials? Saved { get; private set; }

        public Task<CodexOAuthCredentials?> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(credentials);

        public Task SaveAsync(CodexOAuthCredentials credentials, CancellationToken cancellationToken = default)
        {
            Saved = credentials;
            return Task.CompletedTask;
        }
    }
}
