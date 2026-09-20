using BirdyCreditStatus.Core;

namespace BirdyCreditStatus.Core.Tests;

public sealed class FakeClaudeAdapterTests
{
    [Fact]
    public async Task Returns_session_and_week_windows()
    {
        IQuotaAdapter adapter = new FakeClaudeAdapter();

        var result = await adapter.FetchAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal("Claude", result.Snapshot!.Provider);
        Assert.Equal(["Session", "Woche"], result.Snapshot.Windows.Select(w => w.Name));
    }

    [Fact]
    public async Task Failure_mode_returns_na_result()
    {
        IQuotaAdapter adapter = new FakeClaudeAdapter { Mode = FakeMode.NotAvailable };

        var result = await adapter.FetchAsync();

        Assert.False(result.IsSuccess);
        Assert.Null(result.Snapshot);
        Assert.False(result.LoginRequired);
    }

    [Fact]
    public async Task Login_required_mode_returns_setup_state()
    {
        IQuotaAdapter adapter = new FakeClaudeAdapter { Mode = FakeMode.LoginRequired };

        var result = await adapter.FetchAsync();

        Assert.False(result.IsSuccess);
        Assert.Null(result.Snapshot);
        Assert.True(result.LoginRequired);
    }
}
