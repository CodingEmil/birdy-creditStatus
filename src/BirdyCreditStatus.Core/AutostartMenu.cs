namespace BirdyCreditStatus.Core;

/// <summary>Tray-Verdrahtung des Autostart-Hakens gegen <see cref="IAutostartStore"/>:
/// <see cref="IsChecked"/> spiegelt den Store (der Key ist Wahrheit), <see cref="Toggle"/>
/// schaltet über den Store um. Wirft nie — bei Fehler bleibt das Menü bedienbar,
/// Popup/Karten sind unberührt (D011).</summary>
public sealed class AutostartMenu(IAutostartStore store)
{
    private readonly IAutostartStore _store = store;

    /// <summary>Haken-Zustand für das Tray-Menü (spiegelt den Store nach Refresh/Toggle).</summary>
    public bool IsChecked { get; private set; }

    /// <summary>Liest den Store in <see cref="IsChecked"/> ein. Nie Throw.</summary>
    public void Refresh()
    {
        try
        {
            IsChecked = _store.IsEnabled();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            IsChecked = false;
        }
    }

    /// <summary>Schaltet über den Store um und liest die Wahrheit zurück. Nie Throw.</summary>
    public void Toggle()
    {
        try
        {
            _store.SetEnabled(!_store.IsEnabled());
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // Durchfallen zum Refresh: Wahrheit ist der Store, Menü bleibt bedienbar.
        }

        Refresh();
    }
}
