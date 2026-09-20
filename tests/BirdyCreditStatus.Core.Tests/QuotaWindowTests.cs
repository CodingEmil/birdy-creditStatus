using BirdyCreditStatus.Core;

namespace BirdyCreditStatus.Core.Tests;

public sealed class QuotaWindowTests
{
    [Fact]
    public void Default_resets_at_is_null()
    {
        var window = new QuotaWindow("Session", 88);

        Assert.Null(window.ResetsAt);
    }

    [Fact]
    public void Explicit_resets_at_is_preserved()
    {
        var resetsAt = new DateTimeOffset(2026, 9, 20, 12, 0, 0, TimeSpan.Zero);

        var window = new QuotaWindow("Session", 88, resetsAt);

        Assert.Equal(resetsAt, window.ResetsAt);
    }
}
