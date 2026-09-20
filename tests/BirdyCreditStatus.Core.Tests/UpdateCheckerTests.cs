using System.Net;
using System.Text;
using BirdyCreditStatus.Core;

namespace BirdyCreditStatus.Core.Tests;

/// <summary>Naht (F009-T1): Update-Check gegen releases/latest + Download — per Stub-HTTP,
/// nie Throw (offline/kaputt = Unavailable).</summary>
public sealed class UpdateCheckerTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

    private const string NewerJson =
        """{"tag_name":"v2.0.0","assets":[{"name":"birdy-creditStatus-Setup-2.0.0.exe","browser_download_url":"https://x/Setup-2.0.0.exe"}]}""";

    private static HttpClient Stub(HttpStatusCode latestStatus, string latestJson) =>
        new(new RoutingHandler(latestStatus, latestJson));

    [Fact]
    public async Task Newer_tag_reports_available_with_asset_url()
    {
        var checker = new UpdateChecker(Stub(HttpStatusCode.OK, NewerJson));

        var result = await checker.CheckAsync(new Version(1, 4, 0));

        var available = Assert.IsType<UpdateCheckResult.Available>(result);
        Assert.Equal(new Version(2, 0, 0), available.Release.Version);
        Assert.Equal("https://x/Setup-2.0.0.exe", available.Release.DownloadUrl);
    }

    [Fact]
    public async Task Same_or_older_tag_reports_current()
    {
        var checker = new UpdateChecker(Stub(HttpStatusCode.OK, NewerJson));

        Assert.IsType<UpdateCheckResult.Current>(await checker.CheckAsync(new Version(2, 0, 0)));
        Assert.IsType<UpdateCheckResult.Current>(await checker.CheckAsync(new Version(3, 1, 0)));
    }

    [Fact]
    public async Task Missing_asset_or_404_or_garbage_reports_unavailable()
    {
        var noAsset = new UpdateChecker(Stub(HttpStatusCode.OK, """{"tag_name":"v9.9.9","assets":[]}"""));
        var notFound = new UpdateChecker(Stub(HttpStatusCode.NotFound, "{}"));
        var garbage = new UpdateChecker(Stub(HttpStatusCode.OK, "{not json"));
        var badTag = new UpdateChecker(Stub(HttpStatusCode.OK, """{"tag_name":"nonsense","assets":[]}"""));

        Assert.IsType<UpdateCheckResult.Unavailable>(await noAsset.CheckAsync(new Version(1, 0, 0)));
        Assert.IsType<UpdateCheckResult.Unavailable>(await notFound.CheckAsync(new Version(1, 0, 0)));
        Assert.IsType<UpdateCheckResult.Unavailable>(await garbage.CheckAsync(new Version(1, 0, 0)));
        Assert.IsType<UpdateCheckResult.Unavailable>(await badTag.CheckAsync(new Version(1, 0, 0)));
    }

    [Fact]
    public async Task Download_streams_bytes_to_file()
    {
        var checker = new UpdateChecker(Stub(HttpStatusCode.OK, NewerJson));
        var dest = Path.Combine(_directory, "Setup.exe");

        await checker.DownloadAsync("https://x/Setup-2.0.0.exe", dest);

        Assert.Equal(NewerJson, await File.ReadAllTextAsync(dest));
    }

    private sealed class RoutingHandler(HttpStatusCode latestStatus, string latestJson) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var url = request.RequestUri?.ToString() ?? string.Empty;
            var payload = url.Contains("/releases/latest") ? latestJson : NewerJson;
            var status = url.Contains("/releases/latest") ? latestStatus : HttpStatusCode.OK;
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json"),
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
