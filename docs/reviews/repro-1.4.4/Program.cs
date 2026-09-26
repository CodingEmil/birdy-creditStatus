using System.Net;
using BirdyCreditStatus.Core;

// Read-only audit of production code. All files are synthetic and isolated in TEMP;
// all HTTP is handled in memory. No real credentials, network, registry or app UI.
// Assertions express the expected behavior. Original v1.4.4: 1 passed, 8 failed (exit 1).
// After the fixes this same reproducer is expected to pass all 9 checks (exit 0).
internal static class Program
{
    private static async Task<int> Main()
    {
        var cases = new (string Name, Func<Task> Run)[]
        {
            ("CONTROL: valid accounts, validation and refresh work", HealthyControl),
            ("B01: malformed Codex field returns validation error, not exception", () => MalformedAuth(AccountProviders.Codex)),
            ("B01: malformed Claude field returns validation error, not exception", () => MalformedAuth(AccountProviders.Claude)),
            ("B02: null account entry does not crash Load", NullAccount),
            ("B03: failed account write does not report success", FailedAccountWrite),
            ("B04: unavailable cache does not abort successful refresh", LockedCache),
            ("B05: removed account is not restored by in-flight refresh", RemovedDuringRefresh),
            ("B05: older refresh does not overwrite newer snapshot", OutOfOrderRefresh),
            ("B06: shared auth file rotates once for two accounts", SharedAuthRotation),
        };
        var failed = 0;
        foreach (var (name, run) in cases)
        {
            try
            {
                await run();
                Console.WriteLine($"PASS {name}");
            }
            catch (Exception ex)
            {
                failed++;
                Console.WriteLine($"FAIL {name}: {ex.GetType().Name}: {ex.Message}");
            }
        }
        Console.WriteLine($"{cases.Length - failed} passed, {failed} failed. Original v1.4.4 baseline: 1 passed, 8 failed.");
        return failed == 0 ? 0 : 1;
    }

    private static async Task HealthyControl()
    {
        using var temp = new TempFiles();
        var auth = temp.Write("auth.json", """{"tokens":{"access_token":"synthetic-access","refresh_token":"synthetic-refresh"}}""");
        Check(AccountValidator.ValidateAuthFile(AccountProviders.Codex, auth) is null, "Valid fixture rejected");
        var store = temp.AccountStore();
        Check(store.Add(new Account(AccountProviders.Codex, "control", auth)), "Add failed");
        Check(store.Load().Single().Name == "control", "Account not persisted");
        var cache = new SnapshotCache(temp.DirectoryPath);
        var service = new RefreshService(new Dictionary<string, IQuotaAdapter>
        {
            ["control"] = new FixedAdapter(Snapshot("control", 75)),
        }, cache);
        Check((await service.RefreshAllAsync())["control"].IsSuccess, "Refresh failed");
        Check(cache.Load("control")?.Windows.Single().PercentRemaining == 75, "Cache mismatch");
    }

    private static Task MalformedAuth(string provider)
    {
        using var temp = new TempFiles();
        var json = provider == AccountProviders.Codex
            ? """{"tokens":{"access_token":123,"refresh_token":"synthetic-refresh"}}"""
            : """{"claudeAiOauth":{"accessToken":123,"refreshToken":"synthetic-refresh","expiresAt":1}}""";
        var path = temp.Write("invalid-auth.json", json);
        Check(AccountValidator.ValidateAuthFile(provider, path) is not null, "Invalid token accepted");
        return Task.CompletedTask;
    }

    private static Task NullAccount()
    {
        using var temp = new TempFiles();
        temp.Write("accounts.json", "[null]");
        Check(temp.AccountStore().Load().Count == 0, "Invalid entry should be ignored");
        return Task.CompletedTask;
    }

    private static Task FailedAccountWrite()
    {
        using var temp = new TempFiles();
        var store = temp.AccountStore();
        store.Save([new Account(AccountProviders.Codex, "existing", "synthetic-auth.json")]);
        var path = Path.Combine(temp.DirectoryPath, "accounts.json");
        // Read-only attributes reliably prohibit writes on the supported Windows platform.
        File.SetAttributes(path, FileAttributes.ReadOnly);
        var reportedSuccess = store.Add(new Account(AccountProviders.Codex, "new", "synthetic-auth.json"));
        var persisted = store.Load().Any(a => a.Name == "new");
        Check(!reportedSuccess || persisted, "Add returned true, but the new account was not persisted");
        return Task.CompletedTask;
    }

    private static async Task LockedCache()
    {
        using var temp = new TempFiles();
        var cache = new SnapshotCache(temp.DirectoryPath);
        cache.Save(Snapshot("control", 25));
        using var locked = new FileStream(Path.Combine(temp.DirectoryPath, "snapshot.json"),
            FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        var service = new RefreshService(new Dictionary<string, IQuotaAdapter>
        {
            ["control"] = new FixedAdapter(Snapshot("control", 75)),
        }, cache);
        var result = await service.RefreshAllAsync();
        Check(result.ContainsKey("control"), "The card disappeared after a cache write failure");
    }

    private static async Task RemovedDuringRefresh()
    {
        using var temp = new TempFiles();
        var cache = new SnapshotCache(temp.DirectoryPath);
        var adapter = new PendingAdapter();
        var service = new RefreshService(new Dictionary<string, IQuotaAdapter> { ["removed"] = adapter }, cache);
        var pending = service.RefreshAllAsync();
        Check(adapter.Calls == 1, "Fixture did not start the request");
        service.RemoveAdapter("removed");
        cache.Remove("removed");
        adapter.Complete(0, Snapshot("removed", 42));
        await pending;
        Check(cache.Load("removed") is null, "Removed account's snapshot was written back");
    }

    private static async Task OutOfOrderRefresh()
    {
        using var temp = new TempFiles();
        var cache = new SnapshotCache(temp.DirectoryPath);
        var adapter = new PendingAdapter();
        var service = new RefreshService(new Dictionary<string, IQuotaAdapter> { ["control"] = adapter }, cache);
        var older = service.RefreshAllAsync();
        var newer = service.RefreshAllAsync();
        Check(adapter.Calls == 2, "Fixture did not start two requests");
        adapter.Complete(1, Snapshot("control", 90, DateTimeOffset.UnixEpoch.AddMinutes(2)));
        await newer;
        adapter.Complete(0, Snapshot("control", 10, DateTimeOffset.UnixEpoch.AddMinutes(1)));
        await older;
        Check(cache.Load("control")?.Windows.Single().PercentRemaining == 90,
            "Older request replaced the newer 90% snapshot with 10%");
    }

    private static async Task SharedAuthRotation()
    {
        using var temp = new TempFiles();
        var auth = temp.Write("shared-auth.json", """{"tokens":{"access_token":"synthetic-old","refresh_token":"synthetic-once"}}""");
        using var handler = new RotatingTokenServer();
        using var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(10) };
        var adapters = new Dictionary<string, IQuotaAdapter>();
        foreach (var name in new[] { "first", "second" })
        {
            adapters[name] = AccountAdapterFactory.Create(new Account(AccountProviders.Codex, name, auth), http);
        }
        var service = new RefreshService(adapters, new SnapshotCache(temp.DirectoryPath));
        var results = await service.RefreshAllAsync();
        Check(results.Values.All(r => r.IsSuccess) && handler.RefreshCalls == 1,
            $"Refresh POSTs: {handler.RefreshCalls}; false LoginRequired cards: {results.Values.Count(r => r.LoginRequired)}");
    }

    private static QuotaSnapshot Snapshot(string name, double value, DateTimeOffset? fetchedAt = null) =>
        new(name, [new QuotaWindow("5 Stunden", value)], fetchedAt ?? DateTimeOffset.UtcNow);

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class FixedAdapter(QuotaSnapshot snapshot) : IQuotaAdapter
    {
        public Task<QuotaResult> FetchAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new QuotaResult(true, snapshot));
    }

    private sealed class PendingAdapter : IQuotaAdapter
    {
        private readonly List<TaskCompletionSource<QuotaResult>> _calls = [];
        public int Calls => _calls.Count;
        public Task<QuotaResult> FetchAsync(CancellationToken cancellationToken = default)
        {
            var source = new TaskCompletionSource<QuotaResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            _calls.Add(source);
            return source.Task;
        }
        public void Complete(int call, QuotaSnapshot snapshot) => _calls[call].SetResult(new QuotaResult(true, snapshot));
    }

    private sealed class RotatingTokenServer : HttpMessageHandler
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
                    ? Json(HttpStatusCode.OK, """{"access_token":"synthetic-new","refresh_token":"synthetic-rotated"}""")
                    : Json(HttpStatusCode.BadRequest, """{"error":"invalid_grant"}""");
            }
            if (request.Headers.Authorization?.Parameter == "synthetic-old")
            {
                if (Interlocked.Increment(ref _oldRequests) == 2) _bothOldRequests.SetResult();
                await _bothOldRequests.Task.WaitAsync(cancellationToken);
                return Json(HttpStatusCode.Unauthorized, "{}");
            }
            return Json(HttpStatusCode.OK, """{"rate_limit":{"primary_window":{"used_percent":25}}}""");
        }
        private static HttpResponseMessage Json(HttpStatusCode status, string json) =>
            new(status) { Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json") };
    }

    private sealed class TempFiles : IDisposable
    {
        public string DirectoryPath { get; } = Path.Combine(Path.GetTempPath(), "birdy-audit-" + Guid.NewGuid().ToString("N"));
        public TempFiles() => Directory.CreateDirectory(DirectoryPath);
        public string Write(string name, string text)
        {
            var path = Path.Combine(DirectoryPath, name);
            File.WriteAllText(path, text);
            return path;
        }
        public AccountStore AccountStore() => new(DirectoryPath,
            Path.Combine(DirectoryPath, "absent-codex.json"),
            Path.Combine(DirectoryPath, "absent-claude.json"),
            Path.Combine(DirectoryPath, "absent-go.json"));
        public void Dispose()
        {
            foreach (var file in Directory.EnumerateFiles(DirectoryPath)) File.SetAttributes(file, FileAttributes.Normal);
            Directory.Delete(DirectoryPath, recursive: true);
        }
    }
}
