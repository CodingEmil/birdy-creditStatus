using System.Net;
using System.Text;
using BirdyCreditStatus.Core;

namespace BirdyCreditStatus.Core.Tests;

/// <summary>Abnahme-Fluss (F011-T4) ohne XAML: echte Pi-Datei mit 4 Sections →
/// Treffer + Vorschlagsnamen → Validierung → N Konten mit einer Datei →
/// Adapter liefern Fenster, Pi-Datei byte-identisch. Die Checkbox-Liste selbst
/// ist UI-Verdrahtung im Hinzufügen-Dialog (Build + Review).</summary>
public sealed class PiAddFlowTests : IDisposable
{
    private const string CodexJson =
        """{"rate_limit":{"primary_window":{"used_percent":10},"secondary_window":{"used_percent":20}}}""";

    private const string ClaudeJson =
        """{"limits":[{"kind":"session","percent":2},{"kind":"weekly_all","percent":60}]}""";

    private const string GoJson =
        """{"usage":{"rolling":{"status":"ok","percent":30},"weekly":{"status":"ok","percent":45},"monthly":{"status":"ok","percent":12}}}""";

    private readonly string _directory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

    [Fact]
    public async Task Pi_file_yields_four_named_accounts_with_windows_and_identical_bytes()
    {
        var expires = DateTimeOffset.UtcNow.AddYears(1).ToUnixTimeMilliseconds();
        var piAuth = WriteFile(
            "auth.json",
            "{\"anthropic\":{\"access\":\"a\",\"refresh\":\"r\",\"expires\":" + expires + "}," +
            "\"openai-codex\":{\"access\":\"a\",\"refresh\":\"r\",\"accountId\":\"id-1\"}," +
            "\"openai-codex-2\":{\"access\":\"a2\",\"refresh\":\"r2\",\"accountId\":\"id-2\"}," +
            "\"opencode-go\":{\"key\":\"go-1\"}}");
        var before = await File.ReadAllBytesAsync(piAuth);
        var store = new AccountStore(_directory, _missing, _missing, _missing);
        var http = new HttpClient(new RoutingHandler());

        // 1. Treffer + Vorschlagsnamen wie im Dialog.
        var hits = PiAuthDiscovery.DetectFile(piAuth);
        Assert.Equal(4, hits.Count);
        var names = PiAccountNames.Suggest(hits, []);
        Assert.Equal(["Pi Claude", "Pi Codex", "Pi Codex 2", "Pi Go"], names);

        // 2. Format-gültig = anlegbar (kein Abruf nötig).
        for (var i = 0; i < hits.Count; i++)
        {
            Assert.Null(AccountValidator.ValidateName(names[i], store.Load().Select(a => a.Name)));
            Assert.Null(AccountValidator.ValidateAuthFile(hits[i].Provider, hits[i].Path, hits[i].Section));
            Assert.True(store.Add(new Account(hits[i].Provider, names[i], hits[i].Path, hits[i].Section)));
        }

        // 3. Ein Duplikat-Name blockiert nur seine Zeile.
        Assert.NotNull(AccountValidator.ValidateName("Pi Codex", store.Load().Select(a => a.Name)));

        // 4. Karten rendern je Konto, Datei unberührt.
        var windows = new List<string>();
        foreach (var account in store.Load())
        {
            var result = await AccountAdapterFactory.Create(account, http).FetchAsync();
            Assert.True(result.IsSuccess);
            windows.AddRange(result.Snapshot!.Windows.Select(w => w.Name));
        }

        Assert.Equal(["Session", "Woche", "5 Stunden", "Woche", "5 Stunden", "Woche", "Rolling", "Woche", "Monat"], windows);
        Assert.Equal(before, await File.ReadAllBytesAsync(piAuth));
    }

    [Fact]
    public void Invalid_pick_yields_hint_without_accounts()
    {
        var picked = WriteFile("leer.json", """{"openrouter": {"key": "x"}}""");

        Assert.Empty(PiAuthDiscovery.DetectFile(picked));
        Assert.NotNull(AccountValidator.ValidateAuthFile(AccountProviders.Codex, picked));
    }

    private readonly string _missing;

    public PiAddFlowTests()
    {
        _missing = Path.Combine(_directory, "fehlt.json");
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
