using System.Net;
using BirdyCreditStatus.Core;

namespace BirdyCreditStatus.Core.Tests;

public sealed class SharedCredentialRotationTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Codex_accounts_sharing_auth_rotate_once_and_both_succeed(bool alternatePath)
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, "auth.json");
        File.WriteAllText(path, """{"tokens":{"access_token":"old","refresh_token":"once"},"unrelated":true}""");
        var secondPath = alternatePath ? Path.Combine(_directory, ".", "auth.json") : path;
        using var server = new SingleUseTokenServer();
        using var http = new HttpClient(server) { Timeout = TimeSpan.FromSeconds(10) };
        var service = new RefreshService(new Dictionary<string, IQuotaAdapter>
        {
            ["first"] = AccountAdapterFactory.Create(new Account(AccountProviders.Codex, "first", path), http),
            ["second"] = AccountAdapterFactory.Create(new Account(AccountProviders.Codex, "second", secondPath), http),
        }, new SnapshotCache(_directory));

        var results = await service.RefreshAllAsync();

        Assert.Equal(1, server.RefreshCalls);
        Assert.All(results.Values, r => Assert.True(r.IsSuccess));
        Assert.Equal(2, results.Count);
        var stored = await new FileCodexCredentialStore(path).LoadAsync();
        Assert.Equal("rotated", stored!.RefreshToken);
        Assert.Contains("\"unrelated\": true", File.ReadAllText(path));
    }

    [Fact]
    public async Task Claude_accounts_sharing_expired_file_rotate_once()
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, "claude.json");
        File.WriteAllText(path, """{"claudeAiOauth":{"accessToken":"old","refreshToken":"once","expiresAt":1}}""");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var initialReads = new FirstReadBarrier();
        using var server = new ClaudeTokenServer();
        using var http = new HttpClient(server);
        var first = new ClaudeQuotaAdapter(http, new SynchronizedFirstRead(path, initialReads), accountName: "first");
        var second = new ClaudeQuotaAdapter(http, new SynchronizedFirstRead(path, initialReads), accountName: "second");

        var results = await Task.WhenAll(first.FetchAsync(timeout.Token), second.FetchAsync(timeout.Token));

        Assert.Equal(1, server.RefreshCalls);
        Assert.All(results, r => Assert.True(r.IsSuccess));
        Assert.Equal("rotated", (await new FileClaudeCredentialStore(path).LoadAsync())!.RefreshToken);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task File_rotation_gate_is_independent_cancellable_and_released(bool claude)
    {
        Task<IDisposable> Acquire(string file, CancellationToken token = default) => claude
            ? new FileClaudeCredentialStore(Path.Combine(_directory, file)).AcquireRefreshLockAsync(token)
            : new FileCodexCredentialStore(Path.Combine(_directory, file)).AcquireRefreshLockAsync(token);

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var held = await Acquire("shared.json", timeout.Token);
        using (held)
        {
            using var independent = await Acquire("other.json", timeout.Token);
            using var cancelled = new CancellationTokenSource();
            var waiting = Acquire(Path.Combine(".", "shared.json"), cancelled.Token);
            Assert.False(waiting.IsCompleted);
            cancelled.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting);
        }
        held.Dispose(); // Idempotent lease; must not over-release the semaphore.
        using var reacquired = await Acquire("shared.json", timeout.Token);
    }

    private sealed class FirstReadBarrier
    {
        private int _count;
        private readonly TaskCompletionSource _both = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task Arrive(CancellationToken token)
        {
            if (Interlocked.Increment(ref _count) == 2) _both.SetResult();
            return _both.Task.WaitAsync(token);
        }
    }

    // Only controls the first-read ordering. Parsing, locking and saving use the real file store.
    private sealed class SynchronizedFirstRead(string path, FirstReadBarrier barrier) : IClaudeCredentialStore
    {
        private readonly IClaudeCredentialStore _inner = new FileClaudeCredentialStore(path);
        private int _reads;
        public async Task<ClaudeOAuthCredentials?> LoadAsync(CancellationToken cancellationToken = default)
        {
            var result = await _inner.LoadAsync(cancellationToken);
            if (Interlocked.Increment(ref _reads) == 1) await barrier.Arrive(cancellationToken);
            return result;
        }
        public Task SaveAsync(ClaudeOAuthCredentials credentials, CancellationToken cancellationToken = default) =>
            _inner.SaveAsync(credentials, cancellationToken);
        public Task<IDisposable> AcquireRefreshLockAsync(CancellationToken cancellationToken = default) =>
            _inner.AcquireRefreshLockAsync(cancellationToken);
    }

    private sealed class ClaudeTokenServer : HttpMessageHandler
    {
        private int _refreshCalls;
        public int RefreshCalls => _refreshCalls;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Post)
            {
                return Task.FromResult(Interlocked.Increment(ref _refreshCalls) == 1
                    ? Json(HttpStatusCode.OK, """{"access_token":"new","refresh_token":"rotated","expires_in":3600}""")
                    : Json(HttpStatusCode.BadRequest, """{"error":"invalid_grant"}"""));
            }
            return Task.FromResult(Json(HttpStatusCode.OK, """{"limits":[{"kind":"session","percent":25}]}"""));
        }
    }

    private sealed class SingleUseTokenServer : HttpMessageHandler
    {
        private readonly TaskCompletionSource _bothOldRequests = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _oldRequests;
        private int _refreshCalls;
        public int RefreshCalls => _refreshCalls;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Post)
            {
                return Interlocked.Increment(ref _refreshCalls) == 1
                    ? Json(HttpStatusCode.OK, """{"access_token":"new","refresh_token":"rotated"}""")
                    : Json(HttpStatusCode.BadRequest, """{"error":"invalid_grant"}""");
            }
            if (request.Headers.Authorization?.Parameter == "old")
            {
                if (Interlocked.Increment(ref _oldRequests) == 2) _bothOldRequests.SetResult();
                await _bothOldRequests.Task.WaitAsync(cancellationToken);
                return Json(HttpStatusCode.Unauthorized, "{}");
            }
            return Json(HttpStatusCode.OK, """{"rate_limit":{"primary_window":{"used_percent":25}}}""");
        }
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string json) =>
        new(status) { Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json") };

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }
}
