using BirdyCreditStatus.Core;

namespace BirdyCreditStatus.Core.Tests;

/// <summary>Naht (F008-T1): Höhen-Rechenkern — kein Minimum, Cap aus Arbeitshöhe.</summary>
public sealed class PopupSizingTests
{
    [Fact]
    public void Fit_returns_content_height_without_minimum()
    {
        Assert.Equal(120, PopupSizing.Fit(120, 860));
        Assert.Equal(1, PopupSizing.Fit(0.4, 860));
    }

    [Fact]
    public void Fit_clamps_to_max()
    {
        Assert.Equal(860, PopupSizing.Fit(1200, 860));
        Assert.Equal(860, PopupSizing.Fit(860, 860));
    }

    [Fact]
    public void MaxHeight_prefers_860_but_respects_small_work_areas()
    {
        Assert.Equal(860, PopupSizing.MaxHeight(1080));
        Assert.Equal(860, PopupSizing.MaxHeight(892));
        Assert.Equal(600, PopupSizing.MaxHeight(632));
        Assert.Equal(200, PopupSizing.MaxHeight(100));
    }
}
