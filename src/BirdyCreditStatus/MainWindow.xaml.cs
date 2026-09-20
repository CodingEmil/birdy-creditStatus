using System.Windows.Input;
using BirdyCreditStatus.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace BirdyCreditStatus;

/// <summary>Versteckter Host: Tray-Icon plus wiederverwendetes Popup.
/// Klicks kommen ggf. vom Tray-Thread — das Popup marshallt selbst.</summary>
public sealed partial class MainWindow : Window
{
    private readonly HttpClient _http = new();
    private readonly RefreshService _refresh;
    private readonly SnapshotCache _cache = new();
    private readonly IAccountStore _accounts;
    private readonly PopupWindow _popup;
    private readonly AutostartMenu _autostartMenu;

    public ICommand TogglePopupCommand { get; }

    /// <summary>Tray-Menü (#23, F009): natives Win32-Menü der Lib — Klicks kommen nur
    /// über Command an (kein Click/Opening). App-Update prüft GitHub Releases,
    /// Autostart toggelt den Run-Key, Beenden fährt die App herunter.
    /// Werte-Refresh läuft über Popup-Button + Öffnen (D010).</summary>
    public ICommand UpdateAppCommand { get; }

    public ICommand ToggleAutostartCommand { get; }

    public ICommand ExitCommand { get; }

    public MainWindow(IAutostartStore? autostartStore = null, IAccountStore? accountStore = null)
    {
        InitializeComponent();
        _autostartMenu = new AutostartMenu(autostartStore ?? new RegistryAutostartStore());
        AutostartItem.IsChecked = _autostartMenu.IsChecked;
        ToggleAutostartCommand = new RelayCommand(() =>
        {
            _autostartMenu.Toggle();
            AutostartItem.IsChecked = _autostartMenu.IsChecked;
        });
        _accounts = accountStore ?? new AccountStore();
        // F007-T2: ein Adapter je Konto aller Anbieter (Schlüssel = Kontoname),
        // gebaut über die Fabrik; kein Konto = kein Adapter = keine Karte (D012).
        var ordered = AccountOrder.Sort(_accounts.Load().Where(a => !string.IsNullOrWhiteSpace(a.Name)));
        foreach (var account in ordered)
        {
            _cache.Migrate(AccountProviderNames.LegacyCacheKey(account.Provider), account.Name);
        }
        var adapters = new Dictionary<string, IQuotaAdapter>(StringComparer.Ordinal);
        foreach (var account in ordered)
        {
            adapters[account.Name] = AccountAdapterFactory.Create(account, _http);
        }
        // Das Popup rendert den Cache zuerst, dann den parallelen Abruf (D010).
        _refresh = new RefreshService(adapters, _cache);
        _popup = new PopupWindow(_refresh, _cache, _accounts, account => AccountAdapterFactory.Create(account, _http), _http);
        TogglePopupCommand = new RelayCommand(() => _popup.ToggleAtTray());
        UpdateAppCommand = new RelayCommand(async () => await _popup.CheckForAppUpdateAsync());
        ExitCommand = new RelayCommand(() => (Application.Current as App)?.ShutdownApp());
        // Haken-Startzustand: natives Menü liest IsChecked beim Öffnen (kein Opening).

        var warmed = false;
        Activated += async (_, _) =>
        {
            // Einmaliges Aufwärmen mit der exakten Klick-Sequenz, kein Polling (D010).
            if (warmed)
            {
                return;
            }
            warmed = true;
            await Task.Delay(500);
            _popup.PreWarm();
        };
    }

    public void HideHost() => AppWindow.Hide();

    /// <summary>Endgültiges Beenden (Tray-Menü „Beenden"): Popup schließen,
    /// Tray-Icon entfernen, Host schließen.</summary>
    public void Shutdown()
    {
        _popup.Shutdown();
        TrayIcon.Dispose();
        _http.Dispose();
        Close();
    }

    /// <summary>Aktiviert das Popup (Single-Instance-Zweitstart).</summary>
    public void ShowPopup() => _popup.ShowAtTray();
}
