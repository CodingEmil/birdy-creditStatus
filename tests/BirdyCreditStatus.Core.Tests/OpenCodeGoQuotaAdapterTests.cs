using System.Net;
using System.Text;
using BirdyCreditStatus.Core;

namespace BirdyCreditStatus.Core.Tests;

public sealed class OpenCodeGoQuotaAdapterTests
{
    private const string UsageJson =
        """{"usage":{"rolling":{"status":"ok","percent":30,"resetsAt":"2026-09-19T23:43:52.618Z"},"weekly":{"status":"ok","percent":45,"resetsAt":"2026-09-21T00:00:00.000Z"},"monthly":{"status":"ok","percent":12,"resetsAt":"2026-09-21T12:36:29.000Z"}}}""";

    [Fact]
    public async Task Usage_windows_map_to_rolling_week_and_month()
    {
        var adapter = new OpenCodeGoQuotaAdapter(StubHttp(UsageJson), new StubStore("go-key"));

        var result = await adapter.FetchAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal("OpenCode Go", result.Snapshot!.Provider);
        Assert.Equal(["Rolling", "Woche", "Monat"], result.Snapshot.Windows.Select(w => w.Name));
        Assert.Equal([70.0, 55.0, 88.0], result.Snapshot.Windows.Select(w => w.PercentRemaining));
        Assert.Equal(
            DateTimeOffset.Parse("2026-09-19T23:43:52.618Z"),
            result.Snapshot.Windows[0].ResetsAt);
        Assert.Equal(
            DateTimeOffset.Parse("2026-09-21T00:00:00.000Z"),
            result.Snapshot.Windows[1].ResetsAt);
        Assert.Equal(
            DateTimeOffset.Parse("2026-09-21T12:36:29.000Z"),
            result.Snapshot.Windows[2].ResetsAt);
    }

    [Fact]
    public async Task Adapter_with_account_name_uses_it_as_provider()
    {
        var adapter = new OpenCodeGoQuotaAdapter(StubHttp(UsageJson), new StubStore("go-key"), accountName: "Go privat");

        var result = await adapter.FetchAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal("Go privat", result.Snapshot!.Provider);
    }

    [Fact]
    public async Task Only_rolling_window_maps_to_single_card_row()
    {
        var adapter = new OpenCodeGoQuotaAdapter(
            StubHttp("""{"usage":{"rolling":{"status":"ok","percent":20,"resetsAt":"2026-09-19T23:43:52.618Z"}}}"""),
            new StubStore("go-key"));

        var result = await adapter.FetchAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(["Rolling"], result.Snapshot!.Windows.Select(w => w.Name));
        Assert.Equal([80.0], result.Snapshot.Windows.Select(w => w.PercentRemaining));
    }

    [Fact]
    public async Task Only_weekly_window_maps_to_single_card_row()
    {
        var adapter = new OpenCodeGoQuotaAdapter(
            StubHttp("""{"usage":{"weekly":{"status":"ok","percent":50}}}"""),
            new StubStore("go-key"));

        var result = await adapter.FetchAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(["Woche"], result.Snapshot!.Windows.Select(w => w.Name));
        Assert.Null(result.Snapshot.Windows[0].ResetsAt);
    }

    [Fact]
    public async Task Only_monthly_window_maps_to_single_card_row()
    {
        var adapter = new OpenCodeGoQuotaAdapter(
            StubHttp("""{"usage":{"monthly":{"status":"ok","percent":10,"resetsAt":"2026-09-21T12:36:29.000Z"}}}"""),
            new StubStore("go-key"));

        var result = await adapter.FetchAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(["Monat"], result.Snapshot!.Windows.Select(w => w.Name));
        Assert.Equal([90.0], result.Snapshot.Windows.Select(w => w.PercentRemaining));
    }

    [Fact]
    public async Task Rate_limited_counts_as_value()
    {
        var adapter = new OpenCodeGoQuotaAdapter(
            StubHttp("""{"usage":{"rolling":{"status":"rate-limited","percent":99,"resetsAt":"2026-09-19T23:43:52.618Z"}}}"""),
            new StubStore("go-key"));

        var result = await adapter.FetchAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(["Rolling"], result.Snapshot!.Windows.Select(w => w.Name));
        Assert.Equal([1.0], result.Snapshot.Windows.Select(w => w.PercentRemaining));
    }

    [Fact]
    public async Task Unknown_status_window_is_omitted()
    {
        var adapter = new OpenCodeGoQuotaAdapter(
            StubHttp("""{"usage":{"rolling":{"status":"expired","percent":10},"weekly":{"status":"ok","percent":40}}}"""),
            new StubStore("go-key"));

        var result = await adapter.FetchAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(["Woche"], result.Snapshot!.Windows.Select(w => w.Name));
        Assert.Equal([60.0], result.Snapshot.Windows.Select(w => w.PercentRemaining));
    }

    [Fact]
    public async Task Empty_usage_maps_to_not_available()
    {
        var adapter = new OpenCodeGoQuotaAdapter(
            StubHttp("""{"usage":{}}"""),
            new StubStore("go-key"));

        var result = await adapter.FetchAsync();

        Assert.False(result.IsSuccess);
        Assert.Null(result.Snapshot);
        Assert.False(result.LoginRequired);
        Assert.Equal("n/a – Key prüfen / offline", result.Error);
    }

    [Fact]
    public async Task Missing_usage_maps_to_not_available()
    {
        var adapter = new OpenCodeGoQuotaAdapter(
            StubHttp("""{"other":{}}"""),
            new StubStore("go-key"));

        var result = await adapter.FetchAsync();

        Assert.False(result.IsSuccess);
        Assert.Null(result.Snapshot);
        Assert.Equal("n/a – Key prüfen / offline", result.Error);
    }

    [Fact]
    public async Task Unauthorized_maps_to_setup_state()
    {
        var adapter = new OpenCodeGoQuotaAdapter(
            StubHttp("{}", HttpStatusCode.Unauthorized),
            new StubStore("stale-key"));

        var result = await adapter.FetchAsync();

        Assert.False(result.IsSuccess);
        Assert.Null(result.Snapshot);
        Assert.True(result.LoginRequired);
        Assert.Equal("n/a – Key prüfen / offline", result.Error);
    }

    [Fact]
    public async Task Forbidden_maps_to_setup_state()
    {
        var adapter = new OpenCodeGoQuotaAdapter(
            StubHttp("{}", HttpStatusCode.Forbidden),
            new StubStore("stale-key"));

        var result = await adapter.FetchAsync();

        Assert.False(result.IsSuccess);
        Assert.True(result.LoginRequired);
    }

    [Fact]
    public async Task Server_error_maps_to_plain_not_available()
    {
        var adapter = new OpenCodeGoQuotaAdapter(
            StubHttp("{}", HttpStatusCode.InternalServerError),
            new StubStore("go-key"));

        var result = await adapter.FetchAsync();

        Assert.False(result.IsSuccess);
        Assert.False(result.LoginRequired);
        Assert.Equal("n/a – Key prüfen / offline", result.Error);
    }

    [Fact]
    public async Task Offline_and_timeout_map_to_not_available_without_throw()
    {
        var offline = new OpenCodeGoQuotaAdapter(
            new HttpClient(new ThrowingHandler(new HttpRequestException("offline"))),
            new StubStore("go-key"));
        var timeout = new OpenCodeGoQuotaAdapter(
            new HttpClient(new ThrowingHandler(new TaskCanceledException("timeout"))),
            new StubStore("go-key"));

        Assert.False((await offline.FetchAsync()).LoginRequired);
        Assert.Equal("n/a – Key prüfen / offline", (await offline.FetchAsync()).Error);
        Assert.False((await timeout.FetchAsync()).LoginRequired);
        Assert.Equal("n/a – Key prüfen / offline", (await timeout.FetchAsync()).Error);
    }

    [Fact]
    public async Task Invalid_resets_at_keeps_window_without_reset()
    {
        var adapter = new OpenCodeGoQuotaAdapter(
            StubHttp("""{"usage":{"rolling":{"status":"ok","percent":25,"resetsAt":"kein-datum"}}}"""),
            new StubStore("go-key"));

        var result = await adapter.FetchAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(["Rolling"], result.Snapshot!.Windows.Select(w => w.Name));
        Assert.Equal([75.0], result.Snapshot.Windows.Select(w => w.PercentRemaining));
        Assert.Null(result.Snapshot.Windows[0].ResetsAt);
    }

    [Fact]
    public async Task Overuse_clamps_remaining_to_zero()
    {
        var adapter = new OpenCodeGoQuotaAdapter(
            StubHttp("""{"usage":{"rolling":{"status":"ok","percent":130},"weekly":{"status":"ok","percent":-5}}}"""),
            new StubStore("go-key"));

        var result = await adapter.FetchAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal([0.0, 100.0], result.Snapshot!.Windows.Select(w => w.PercentRemaining));
    }

    [Fact]
    public async Task Sends_bearer_and_accept_headers_to_usage_path()
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
        var adapter = new OpenCodeGoQuotaAdapter(new HttpClient(handler), new StubStore("go-key"));

        await adapter.FetchAsync();

        Assert.NotNull(usageRequest);
        Assert.Equal("https://opencode.ai/zen/go/v1/usage", usageRequest.RequestUri!.ToString());
        Assert.Equal("Bearer", usageRequest.Headers.Authorization?.Scheme);
        Assert.Equal("go-key", usageRequest.Headers.Authorization?.Parameter);
        Assert.Contains("application/json", usageRequest.Headers.Accept.Select(h => h.MediaType));
    }

    [Fact]
    public async Task Missing_key_maps_to_setup_state_without_http()
    {
        var calls = 0;
        var handler = new SequenceHandler((_, _) =>
        {
            calls++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(UsageJson, Encoding.UTF8, "application/json"),
            });
        });
        var adapter = new OpenCodeGoQuotaAdapter(new HttpClient(handler), new StubStore(null));

        var result = await adapter.FetchAsync();

        Assert.False(result.IsSuccess);
        Assert.True(result.LoginRequired);
        Assert.Equal("n/a – Key prüfen / offline", result.Error);
        Assert.Equal(0, calls);
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

    private sealed class StubStore(string? apiKey) : IOpenCodeGoCredentialStore
    {
        public Task<string?> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(apiKey);
    }
}
