using BirdyCreditStatus.Core;

namespace BirdyCreditStatus.Core.Tests;

public sealed class CardDisplayBuilderTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 19, 18, 0, 0, TimeSpan.Zero);

    private static QuotaSnapshot ClaudeSnapshot() => new(
        "Claude",
        [new QuotaWindow("Session", 88, Now.AddHours(4)), new QuotaWindow("Woche", 40)],
        Now);

    [Fact]
    public void Success_maps_rows_resets_and_timestamp()
    {
        var display = CardDisplayBuilder.Build(
            "Claude", new QuotaResult(true, ClaudeSnapshot()), null, Now);

        Assert.Equal("Claude", display.Provider);
        Assert.Equal(["Session", "Woche"], display.Rows.Select(r => r.WindowName));
        Assert.Equal(["88 % übrig", "40 % übrig"], display.Rows.Select(r => r.ValueText));
        Assert.Equal([88.0, 40.0], display.Rows.Select(r => r.BarValue));
        Assert.Equal("in 4 Stunden", display.Rows[0].ResetText);
        Assert.Equal("", display.Rows[1].ResetText);
        Assert.Contains(ClaudeSnapshot().FetchedAt.LocalDateTime.ToString("HH:mm"), display.UpdatedText);
        Assert.Null(display.ErrorText);
        Assert.False(display.ShowSetup);
    }

    [Fact]
    public void Failure_keeps_stale_cache_rows_and_sets_error()
    {
        var cached = ClaudeSnapshot();
        var display = CardDisplayBuilder.Build(
            "Claude", new QuotaResult(false, null, "n/a – Key prüfen / offline"), cached, Now);

        Assert.Equal(["88 % übrig", "40 % übrig"], display.Rows.Select(r => r.ValueText));
        Assert.Contains(ClaudeSnapshot().FetchedAt.LocalDateTime.ToString("HH:mm"), display.UpdatedText);
        Assert.Equal("n/a – Key prüfen / offline", display.ErrorText);
        Assert.False(display.ShowSetup);
    }

    [Fact]
    public void Failure_without_cache_shows_only_error()
    {
        var display = CardDisplayBuilder.Build(
            "Codex", new QuotaResult(false, null, "n/a – Key prüfen / offline"), null, Now);

        Assert.Empty(display.Rows);
        Assert.Null(display.UpdatedText);
        Assert.Equal("n/a – Key prüfen / offline", display.ErrorText);
        Assert.False(display.ShowSetup);
    }

    [Fact]
    public void Setup_signal_shows_setup_without_rows_or_error()
    {
        var display = CardDisplayBuilder.Build(
            "Codex", new QuotaResult(false, null, "n/a – Key prüfen / offline", LoginRequired: true), null, Now);

        Assert.True(display.ShowSetup);
        Assert.Empty(display.Rows);
        Assert.Null(display.ErrorText);
        Assert.Null(display.UpdatedText);
    }

    [Fact]
    public void Cache_only_render_shows_stale_rows_without_error()
    {
        var display = CardDisplayBuilder.Build("Codex", null, ClaudeSnapshot(), Now);

        Assert.Equal(2, display.Rows.Count);
        Assert.Contains(ClaudeSnapshot().FetchedAt.LocalDateTime.ToString("HH:mm"), display.UpdatedText);
        Assert.Null(display.ErrorText);
        Assert.False(display.ShowSetup);
    }

    [Fact]
    public void Empty_state_has_no_rows_texts_or_setup()
    {
        var display = CardDisplayBuilder.Build("Codex", null, null, Now);

        Assert.Empty(display.Rows);
        Assert.Null(display.UpdatedText);
        Assert.Null(display.ErrorText);
        Assert.False(display.ShowSetup);
    }

    [Fact]
    public void Go_success_maps_three_rows_with_resets_and_timestamp()
    {
        var snapshot = new QuotaSnapshot(
            "OpenCode Go",
            [new QuotaWindow("Rolling", 64, Now.AddHours(2)), new QuotaWindow("Woche", 48), new QuotaWindow("Monat", 81, Now.AddDays(3))],
            Now);
        var display = CardDisplayBuilder.Build("OpenCode Go", new QuotaResult(true, snapshot), null, Now);

        Assert.Equal(["Rolling", "Woche", "Monat"], display.Rows.Select(r => r.WindowName));
        Assert.Equal(["64 % übrig", "48 % übrig", "81 % übrig"], display.Rows.Select(r => r.ValueText));
        Assert.Equal([64.0, 48.0, 81.0], display.Rows.Select(r => r.BarValue));
        Assert.Equal("in 2 Stunden", display.Rows[0].ResetText);
        Assert.Equal("", display.Rows[1].ResetText);
        Assert.Equal("in 3 Tagen", display.Rows[2].ResetText);
        Assert.Contains(Now.LocalDateTime.ToString("HH:mm"), display.UpdatedText);
        Assert.Null(display.ErrorText);
        Assert.False(display.ShowSetup);
    }

    [Fact]
    public void Go_setup_signal_shows_setup_without_rows_or_error()
    {
        var display = CardDisplayBuilder.Build(
            "OpenCode Go", new QuotaResult(false, null, "n/a – Key prüfen / offline", LoginRequired: true), null, Now);

        Assert.True(display.ShowSetup);
        Assert.Empty(display.Rows);
        Assert.Null(display.ErrorText);
        Assert.Null(display.UpdatedText);
    }

    [Fact]
    public void Go_failure_keeps_stale_cache_rows_and_sets_error()
    {
        var cached = new QuotaSnapshot(
            "OpenCode Go",
            [new QuotaWindow("Rolling", 64)],
            Now);
        var display = CardDisplayBuilder.Build(
            "OpenCode Go", new QuotaResult(false, null, "n/a – Key prüfen / offline"), cached, Now);

        Assert.Equal(["64 % übrig"], display.Rows.Select(r => r.ValueText));
        Assert.Contains(Now.LocalDateTime.ToString("HH:mm"), display.UpdatedText);
        Assert.Equal("n/a – Key prüfen / offline", display.ErrorText);
        Assert.False(display.ShowSetup);
    }
}
