using BirdyCreditStatus.Core;

namespace BirdyCreditStatus.Core.Tests;

public sealed class PopupPlacementTests
{
    private static readonly ScreenArea Primary = new(0, 0, 3440, 1440);
    private static readonly ScreenArea Secondary = new(3440, 0, 2560, 1440);

    [Fact]
    public void Cursor_on_secondary_picks_secondary_work_area()
    {
        var picked = PopupPlacement.PickWorkArea([Primary, Secondary], 5000, 500, Primary);

        Assert.Equal(Secondary, picked);
    }

    [Fact]
    public void Cursor_on_primary_picks_primary_work_area()
    {
        var picked = PopupPlacement.PickWorkArea([Primary, Secondary], 1973, 137, Secondary);

        Assert.Equal(Primary, picked);
    }

    [Fact]
    public void Cursor_outside_all_falls_back()
    {
        var picked = PopupPlacement.PickWorkArea([Primary, Secondary], -100, -100, Primary);

        Assert.Equal(Primary, picked);
    }

    [Fact]
    public void Bottom_right_anchors_inside_work_area_with_uniform_margin()
    {
        var (x, y) = PopupPlacement.BottomRight(Secondary, windowWidth: 380, windowHeight: 680);

        Assert.Equal(3440 + 2560 - 396, x);
        Assert.Equal(0 + 1440 - 696, y);
    }
}
