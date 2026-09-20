using System.Net;
using System.Text;
using BirdyCreditStatus.Core;

namespace BirdyCreditStatus.Core.Tests;

public sealed class ClaudeResetsAtTests
{
    [Fact]
    public async Task Resets_at_iso_dates_map_to_windows()
    {
        var adapter = new ClaudeQuotaAdapter(
            StubHttp("""{"limits":[{"kind":"session","percent":2,"resets_at":"2026-09-19T22:20:00.39+00:00"},{"kind":"weekly_all","percent":60,"resets_at":"2026-09-23T10:00:00.39+00:00"}]}"""),
            new StubStore(Fresh()));

        var result = await adapter.FetchAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(
            new DateTimeOffset(2026, 9, 19, 22, 20, 0, TimeSpan.Zero).AddMilliseconds(390),
            result.Snapshot!.Windows[0].ResetsAt);
        Assert.Equal(
            new DateTimeOffset(2026, 9, 23, 10, 0, 0, TimeSpan.Zero).AddMilliseconds(390),
            result.Snapshot.Windows[1].ResetsAt);
    }

    [Fact]
    public async Task Missing_or_invalid_resets_at_maps_to_null()
    {
        var adapter = new ClaudeQuotaAdapter(
            StubHttp("""{"limits":[{"kind":"session","percent":2},{"kind":"weekly_all","percent":60,"resets_at":"kein-datum"}]}"""),
            new StubStore(Fresh()));

        var result = await adapter.FetchAsync();

        Assert.True(result.IsSuccess);
        Assert.Null(result.Snapshot!.Windows[0].ResetsAt);
        Assert.Null(result.Snapshot.Windows[1].ResetsAt);
    }

    private static HttpClient StubHttp(string json) =>
        new(new StubHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        }));

    private sealed class StubHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(response);
    }

    private static ClaudeOAuthCredentials Fresh() =>
        new("a", "r", DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeMilliseconds());

    private sealed class StubStore(ClaudeOAuthCredentials? credentials) : IClaudeCredentialStore
    {
        public Task<ClaudeOAuthCredentials?> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(credentials);

        public Task SaveAsync(ClaudeOAuthCredentials credentials, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
