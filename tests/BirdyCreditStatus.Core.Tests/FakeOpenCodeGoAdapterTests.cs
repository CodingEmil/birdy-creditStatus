using BirdyCreditStatus.Core;

namespace BirdyCreditStatus.Core.Tests;

public sealed class FakeOpenCodeGoAdapterTests
{
    [Fact]
    public async Task Returns_rolling_week_and_month_windows()
    {
        IQuotaAdapter adapter = new FakeOpenCodeGoAdapter();

        var result = await adapter.FetchAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal("OpenCode Go", result.Snapshot!.Provider);
        Assert.Equal(["Rolling", "Woche", "Monat"], result.Snapshot.Windows.Select(w => w.Name));
    }

    [Fact]
    public async Task Failure_mode_returns_na_result()
    {
        IQuotaAdapter adapter = new FakeOpenCodeGoAdapter { Mode = FakeMode.NotAvailable };

        var result = await adapter.FetchAsync();

        Assert.False(result.IsSuccess);
        Assert.Null(result.Snapshot);
        Assert.False(result.LoginRequired);
    }

    [Fact]
    public async Task Login_required_mode_returns_setup_state()
    {
        IQuotaAdapter adapter = new FakeOpenCodeGoAdapter { Mode = FakeMode.LoginRequired };

        var result = await adapter.FetchAsync();

        Assert.False(result.IsSuccess);
        Assert.Null(result.Snapshot);
        Assert.True(result.LoginRequired);
    }
}
