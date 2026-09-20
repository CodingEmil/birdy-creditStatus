using System.Net;
using System.Text;
using BirdyCreditStatus.Core;

namespace BirdyCreditStatus.Core.Tests;

public sealed class ClaudeQuotaAdapterTests
{
    private const string UsageJson =
        """{"limits":[{"kind":"session","percent":2,"resets_at":"2026-09-19T22:20:00.39+00:00"},{"kind":"weekly_all","percent":60,"resets_at":"2026-09-23T10:00:00.39+00:00"}]}""";

    [Fact]
    public async Task Usage_limits_map_to_session_and_week()
    {
        var adapter = new ClaudeQuotaAdapter(
            StubHttp(UsageJson),
            new StubStore(new ClaudeOAuthCredentials("access", "refresh", FarFuture())));

        var result = await adapter.FetchAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal("Claude", result.Snapshot!.Provider);
        Assert.Equal(["Session", "Woche"], result.Snapshot.Windows.Select(w => w.Name));
        Assert.Equal([98.0, 40.0], result.Snapshot.Windows.Select(w => w.PercentRemaining));
    }

    [Fact]
    public async Task Unknown_kinds_ignored_empty_limits_are_not_available()
    {
        var adapter = new ClaudeQuotaAdapter(
            StubHttp("""{"limits":[{"kind":"other","percent":99}]}"""),
            new StubStore(new ClaudeOAuthCredentials("access", "refresh", FarFuture())));

        var result = await adapter.FetchAsync();

        Assert.False(result.IsSuccess);
        Assert.Null(result.Snapshot);
        Assert.Equal("n/a – Key prüfen / offline", result.Error);
    }

    [Fact]
    public async Task Overuse_clamps_remaining_to_zero()
    {
        var adapter = new ClaudeQuotaAdapter(
            StubHttp("""{"limits":[{"kind":"session","percent":130},{"kind":"weekly_all","percent":100}]}"""),
            new StubStore(new ClaudeOAuthCredentials("access", "refresh", FarFuture())));

        var result = await adapter.FetchAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal([0.0, 0.0], result.Snapshot!.Windows.Select(w => w.PercentRemaining));
    }

    [Fact]
    public async Task Adapter_with_account_name_uses_it_as_provider()
    {
        var adapter = new ClaudeQuotaAdapter(
            StubHttp(UsageJson),
            new StubStore(new ClaudeOAuthCredentials("access", "refresh", FarFuture())),
            accountName: "Claude privat");

        var result = await adapter.FetchAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal("Claude privat", result.Snapshot!.Provider);
    }

    [Fact]
    public async Task Unauthorized_maps_to_not_available_without_throw()
    {
        var adapter = new ClaudeQuotaAdapter(
            StubHttp("{}", HttpStatusCode.Unauthorized),
            new StubStore(new ClaudeOAuthCredentials("access", "refresh", FarFuture())));

        var result = await adapter.FetchAsync();

        Assert.False(result.IsSuccess);
        Assert.Null(result.Snapshot);
        Assert.Equal("n/a – Key prüfen / offline", result.Error);
    }

    [Fact]
    public async Task Offline_maps_to_not_available_without_throw()
    {
        var adapter = new ClaudeQuotaAdapter(
            new HttpClient(new ThrowingHandler(new HttpRequestException("offline"))),
            new StubStore(new ClaudeOAuthCredentials("access", "refresh", FarFuture())));

        var result = await adapter.FetchAsync();

        Assert.False(result.IsSuccess);
        Assert.Equal("n/a – Key prüfen / offline", result.Error);
    }

    [Fact]
    public async Task Expired_token_refreshes_transparently_and_retries_usage()
    {
        HttpRequestMessage? refreshRequest = null;
        string? refreshBody = null;
        HttpRequestMessage? usageRequest = null;
        var store = new StubStore(new ClaudeOAuthCredentials("old-access", "refresh-123", Expired()));
        var handler = new SequenceHandler(async (request, _) =>
        {
            if (request.RequestUri!.AbsoluteUri.Contains("oauth/token"))
            {
                refreshRequest = request;
                refreshBody = await request.Content!.ReadAsStringAsync();
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        """{"access_token":"new-access","refresh_token":"refresh-123","expires_in":3600}""",
                        Encoding.UTF8, "application/json"),
                };
            }

            usageRequest = request;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(UsageJson, Encoding.UTF8, "application/json"),
            };
        });
        var adapter = new ClaudeQuotaAdapter(new HttpClient(handler), store, "https://example.test/v1/oauth/token");

        var result = await adapter.FetchAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal([98.0, 40.0], result.Snapshot!.Windows.Select(w => w.PercentRemaining));
        Assert.NotNull(refreshRequest);
        Assert.Contains("refresh_token", refreshBody);
        Assert.Contains(ClaudeQuotaAdapter.ClientId, refreshBody);
        Assert.Equal("new-access", store.Saved?.AccessToken);
        Assert.Equal("Bearer", usageRequest!.Headers.Authorization?.Scheme);
        Assert.Equal("new-access", usageRequest.Headers.Authorization?.Parameter);
    }

    [Fact]
    public async Task Failed_refresh_maps_to_not_available()
    {
        var handler = new SequenceHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json"),
        }));
        var adapter = new ClaudeQuotaAdapter(
            new HttpClient(handler),
            new StubStore(new ClaudeOAuthCredentials("old-access", "refresh-123", Expired())),
            "https://example.test/v1/oauth/token");

        var result = await adapter.FetchAsync();

        Assert.False(result.IsSuccess);
        Assert.Equal("n/a – Key prüfen / offline", result.Error);
    }

    [Fact]
    public async Task Missing_login_maps_to_setup_state()
    {
        var adapter = new ClaudeQuotaAdapter(StubHttp(UsageJson), new StubStore(null));

        var result = await adapter.FetchAsync();

        Assert.False(result.IsSuccess);
        Assert.Equal("n/a – Key prüfen / offline", result.Error);
        Assert.True(result.LoginRequired);
    }

    [Fact]
    public async Task Provider_failure_is_not_a_setup_state()
    {
        var unauthorized = new ClaudeQuotaAdapter(
            StubHttp("{}", HttpStatusCode.Unauthorized),
            new StubStore(new ClaudeOAuthCredentials("access", "refresh", FarFuture())));
        var offline = new ClaudeQuotaAdapter(
            new HttpClient(new ThrowingHandler(new HttpRequestException("offline"))),
            new StubStore(new ClaudeOAuthCredentials("access", "refresh", FarFuture())));

        Assert.False((await unauthorized.FetchAsync()).LoginRequired);
        Assert.False((await offline.FetchAsync()).LoginRequired);
    }

    private static HttpClient StubHttp(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new(new StubHandler(new HttpResponseMessage(status)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        }));

    private static long FarFuture() =>
        DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeMilliseconds();

    private static long Expired() =>
        DateTimeOffset.UtcNow.AddHours(-1).ToUnixTimeMilliseconds();

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

    private sealed class StubStore(ClaudeOAuthCredentials? credentials) : IClaudeCredentialStore
    {
        public ClaudeOAuthCredentials? Saved { get; private set; }

        public Task<ClaudeOAuthCredentials?> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(credentials);

        public Task SaveAsync(ClaudeOAuthCredentials credentials, CancellationToken cancellationToken = default)
        {
            Saved = credentials;
            return Task.CompletedTask;
        }
    }
}
