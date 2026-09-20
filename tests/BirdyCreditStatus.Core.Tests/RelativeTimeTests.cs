using BirdyCreditStatus.Core;

namespace BirdyCreditStatus.Core.Tests;

public sealed class RelativeTimeTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 19, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Null_shows_nothing() => Assert.Equal("", RelativeTime.Format(null, Now));

    [Fact]
    public void Past_shows_due() =>
        Assert.Equal("gleich", RelativeTime.Format(Now.AddMinutes(-1), Now));

    [Theory]
    [InlineData(1, "in 1 Minute")]
    [InlineData(20, "in 20 Minuten")]
    [InlineData(59, "in 59 Minuten")]
    public void Minutes(int minutes, string expected) =>
        Assert.Equal(expected, RelativeTime.Format(Now.AddMinutes(minutes), Now));

    [Theory]
    [InlineData(60, "in 1 Stunde")]
    [InlineData(274, "in 4 Stunden")]
    public void Hours(int minutes, string expected) =>
        Assert.Equal(expected, RelativeTime.Format(Now.AddMinutes(minutes), Now));

    [Theory]
    [InlineData(2, "in 2 Tagen")]
    [InlineData(1, "in 1 Tag")]
    public void Days(int days, string expected) =>
        Assert.Equal(expected, RelativeTime.Format(Now.AddDays(days), Now));
}
