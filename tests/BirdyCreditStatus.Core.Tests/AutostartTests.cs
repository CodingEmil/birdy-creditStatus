using BirdyCreditStatus.Core;

namespace BirdyCreditStatus.Core.Tests;

/// <summary>Naht-Tests für F005-T1 (#17): Autostart-Semantik hinter
/// <see cref="IAutostartStore"/> plus Tray-Verdrahtung (<see cref="AutostartMenu"/>)
/// gegen den In-Memory-Fake — ohne echte Registry (D011).</summary>
public sealed class AutostartTests
{
    [Fact]
    public void Fresh_store_is_disabled_missing_key_means_off()
    {
        Assert.False(new FakeAutostartStore().IsEnabled());
    }

    [Fact]
    public void SetEnabled_toggles_on_and_off()
    {
        var store = new FakeAutostartStore();

        store.SetEnabled(true);
        Assert.True(store.IsEnabled());

        store.SetEnabled(false);
        Assert.False(store.IsEnabled());
    }

    [Fact]
    public void SetEnabled_is_idempotent()
    {
        var store = new FakeAutostartStore();

        store.SetEnabled(true);
        store.SetEnabled(true);
        Assert.True(store.IsEnabled());

        store.SetEnabled(false);
        store.SetEnabled(false);
        Assert.False(store.IsEnabled());
    }

    [Fact]
    public void Failing_store_never_throws()
    {
        var store = new FakeAutostartStore { FailWrites = true };

        var exception = Record.Exception(() =>
        {
            store.SetEnabled(true);
            store.IsEnabled();
            store.SetEnabled(false);
        });

        Assert.Null(exception);
    }

    [Fact]
    public void Menu_refresh_reflects_store()
    {
        var store = new FakeAutostartStore();
        var menu = new AutostartMenu(store);

        Assert.False(menu.IsChecked);

        store.SetEnabled(true);
        menu.Refresh();

        Assert.True(menu.IsChecked);
    }

    [Fact]
    public void Menu_toggle_switches_store_and_state()
    {
        var menu = new AutostartMenu(new FakeAutostartStore());

        menu.Toggle();
        Assert.True(menu.IsChecked);

        menu.Toggle();
        Assert.False(menu.IsChecked);
    }

    [Fact]
    public void Menu_stays_usable_when_store_fails()
    {
        var menu = new AutostartMenu(new FakeAutostartStore { FailWrites = true });

        var exception = Record.Exception(() =>
        {
            menu.Toggle();
            menu.Refresh();
            menu.Toggle();
        });

        Assert.Null(exception);
        // Menü bleibt bedienbar: Zustand folgt der Wahrheit des Stores (aus).
        Assert.False(menu.IsChecked);
    }
}
