using System.Diagnostics;
using System.Runtime.InteropServices;
using BirdyCreditStatus.Core;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;

namespace BirdyCreditStatus;

/// <summary>Tray-Popup (Mica-Flyout). Alle Einstiege sind Thread-sicher:
/// Aufrufe abseits des UI-Threads werden gemarshallt.</summary>
public sealed partial class PopupWindow : Window
{
    private readonly RefreshService _refresh;
    private readonly SnapshotCache _cache;
    private readonly IAccountStore _accounts;
    private readonly Func<Account, IQuotaAdapter> _adapterFactory;
    private readonly HttpClient _http;
    private readonly DispatcherQueue _dispatcher;
    private readonly Dictionary<string, ExtraAccountCard> _extraAccountCards = new(StringComparer.Ordinal);
    // Zuletzt geordnete Konten des aktuellen Render-Durchgangs (F007-T2): Helfer lesen
    // dieses Feld statt wiederholt die Datei zu laden (ein Load pro Öffnen/Refresh).
    private bool _shown;
    private bool _wasActive;
    // F006-T3: Während der Dateiauswahl (FileOpenPicker) darf das Popup weder per
    // Deactivated noch per Maus-Hook schließen — der Picker ist ein eigenes Fenster,
    // das Popup verliert zwangsläufig den Fokus, gehört aber weiter dazu.
    // Normales Schließen (X, Fokusverlust Ersatz) versteckt nur — echtes Beenden
    // läuft über Shutdown() und erlaubt das Schließen via _allowClose. Ohne das
    // bricht der Closing-Handler auch Application.Exit() ab (Prozess lebt weiter).
    // Nur nach echter Aktivierung darf Deactivated schließen: Ein frisch per
    // Tray geöffnetes Popup ist ggf. nie aktiv geworden (Foreground-Lock,
    // z. B. Spiel im Vordergrund) — ein sofortiges Deactivated darf es nicht
    // gleich wieder schließen (Flackern). Der Maus-Hook fängt Klicks daneben
    // auch ohne Fokus ab.

    public PopupWindow(
        RefreshService refresh,
        SnapshotCache cache,
        IAccountStore? accountStore = null,
        Func<Account, IQuotaAdapter>? adapterFactory = null,
        HttpClient? http = null)
    {
        InitializeComponent();
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "TrayIcon.ico"));
        _refresh = refresh;
        _cache = cache;
        _accounts = accountStore ?? new AccountStore();
        // Adapter-Fabrik (F006-T3): MainWindow teilt seine HttpClient-gebundene Fabrik,
        // Fallback nur für alte Aufrufstellen ohne Fabrik.
        _adapterFactory = adapterFactory ?? (account => new CodexQuotaAdapter(
            new HttpClient(),
            string.IsNullOrWhiteSpace(account.AuthFilePath)
                ? new FileCodexCredentialStore()
                : new FileCodexCredentialStore(account.AuthFilePath),
            accountName: account.Name));
        _dispatcher = DispatcherQueue.GetForCurrentThread();
        _http = http ?? new HttpClient();
        // Laufzeit-Version im Footer (welcher Build läuft gerade?).
        try
        {
            AppVersionText.Text = AppVersion.Format(
                System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            AppVersionText.Text = AppVersion.Format(null);
        }

        // T5-Fix (D007): ThemeShadow absichtlich nicht verdrahtet — Receivers.Add
        // ließ den Prozess reproduzierbar nativ in Microsoft.UI.Xaml.dll sterben
        // (0xC000027B beim Start, 4x Event-Id 1000). Schweben kommt aus Card-Fill
        // + Stroke über Acrylic; echter Schatten erst nach verifiziertem Ansatz.

        var presenter = OverlappedPresenter.Create();
        presenter.IsResizable = false;
        presenter.IsMinimizable = false;
        presenter.IsMaximizable = false;
        // Tray-Flyout-Vertrag: Bei Klick immer sichtbar — auch wenn Windows die
        // Vordergrund-Aktivierung verweigert (z. B. randloses Spiel auf dem Klick-Monitor).
        // Komponiert mit FlyoutStyle (Toolwindow-Bit, Rundung, dunkle Rahmenfarbe).
        presenter.IsAlwaysOnTop = true;
        // Rahmen ja (1px, dunkel per RefreshChrome), Titelleiste nein: So malt DWM
        // einen echten, farbigen Rahmen statt des weißen Default-Saums. Rohes Strippen
        // von WS_BORDER verliert gegen AppWindow (stellt Bits kommentarlos wieder her).
        presenter.SetBorderAndTitleBar(true, false);
        AppWindow.SetPresenter(presenter);
        AppWindow.Resize(new SizeInt32(380, 860));

        FlyoutStyle.Apply(this);
        // Backdrop genau EINMAL setzen: Jedes erneute Zuweisen von SystemBackdrop
        // reißt den Composition-Backdrop ab und baut ihn neu (sichtbares Flackern),
        // und SetWindowPos mit FRAMECHANGED (FlyoutStyle.Apply) löst ihn zusätzlich
        // vom Fenster. Darum: Apply einmal hier, danach nur noch RefreshChrome
        // (reines DwmSetWindowAttribute, ohne FRAMECHANGED) — nie wieder anfassen.
        SystemBackdrop = CreatePopupBackdrop();
        AppWindow.Changed += (_, _) => FlyoutStyle.RefreshChrome(this);
        Activated += (_, e) =>
        {
            if (e.WindowActivationState == WindowActivationState.Deactivated)
            {
                // Dateiauswahl läuft (eigenes Picker-Fenster): nicht schließen (F006-T3).
                if (_suspendDismiss)
                {
                    return;
                }

                // Nur schließen, wenn das Popup zuvor wirklich aktiv war (s. Feld).
                var shouldHide = _wasActive && _shown;
                _wasActive = false;
                if (shouldHide)
                {
                    HidePopup();
                }
                return;
            }

            _wasActive = true;
            FlyoutStyle.RefreshChrome(this);
        };
        AppWindow.Closing += (_, args) =>
        {
            // X versteckt nur (Grill-Entscheid); echtes Beenden nur per Tray-Menü
            // via Shutdown() — das setzt _allowClose, sonst käme Exit() nie durch.
            if (_allowClose)
            {
                return;
            }
            args.Cancel = true;
            HidePopup();
        };
    }

    /// <summary>T4 Acrylic-Shell: DesktopAcrylicBackdrop als Standard (dunkles Frosted-Glass).
    /// Mica-Fallback (Stelle: Rückfall unten), wenn Acrylic auf dem System nicht
    /// verfügbar ist (älteres Windows, deaktivierte Transparenz, Richtlinie).</summary>
    private static SystemBackdrop CreatePopupBackdrop()
    {
        // DesktopAcrylicController.IsSupported() ist der dokumentierte
        // Verfügbarkeitscheck; try/catch schützt zusätzlich vor unerwarteten
        // Composition-Fehlern beim Erzeugen des Backdrops.
        try
        {
            if (DesktopAcrylicController.IsSupported())
            {
                return new DesktopAcrylicBackdrop();
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // Fallthrough zum Mica-Fallback unten.
        }

        // MICA-FALLBACK-STELLE (T4): Acrylic nicht verfügbar → dunkles Mica (BaseAlt).
        return new MicaBackdrop { Kind = MicaKind.BaseAlt };
    }

    public void ToggleAtTray()
    {
        if (_dispatcher.HasThreadAccess)
        {
            ToggleCore();
        }
        else
        {
            _dispatcher.TryEnqueue(ToggleCore);
        }
    }

    /// <summary>Einmaliges Aufwärmen mit der exakten Klick-Sequenz (unsichtbar).</summary>
    public void PreWarm()
    {
        if (_dispatcher.HasThreadAccess)
        {
            PreWarmCore();
        }
        else
        {
            _dispatcher.TryEnqueue(PreWarmCore);
        }
    }

    /// <summary>Stellt das Popup sicher sichtbar (Zweitstart-Aktivierung).</summary>
    public void ShowAtTray()
    {
        if (_dispatcher.HasThreadAccess)
        {
            ShowIfHiddenCore();
        }
        else
        {
            _dispatcher.TryEnqueue(ShowIfHiddenCore);
        }
    }

    public void Render(IReadOnlyDictionary<string, QuotaResult> results)
    {
        if (_dispatcher.HasThreadAccess)
        {
            UpdateUi(results);
        }
        else
        {
            _dispatcher.TryEnqueue(() => UpdateUi(results));
        }
    }

    private void ToggleCore()
    {
        if (_shown)
        {
            HidePopup();
        }
        else
        {
            ShowCore();
        }
    }

    private void ShowIfHiddenCore()
    {
        if (_shown)
        {
            Activate();
            return;
        }

        ShowCore();
    }

    private async void ShowCore()
    {
        MoveToBottomRight();
        RenderCached();
        _shown = true;
        _wasActive = false;
        HookMouse();
        FlyoutStyle.RefreshChrome(this);
        Activate();
        EnsureForeground();
        Render(await _refresh.RefreshAllAsync());
    }

    private void PreWarmCore()
    {
        AppWindow.Move(new PointInt32(-10000, -10000));
        Activate();
        FlyoutStyle.RefreshChrome(this);
        RenderCached();
        AppWindow.Hide();
        _shown = false;
        _wasActive = false;
        MoveToBottomRight();
    }

    private void HidePopup()
    {
        _shown = false;
        _wasActive = false;
        UnhookMouse();
        AppWindow.Hide();
    }

    private bool _allowClose;

    // Win32-Vordergrund (D009): Window.Activate() allein liefert nicht zuverlässig
    // echten Win32-Foreground (Foreground-Lock) — WinUI meldet CodeActivated, doch
    // GetForegroundWindow() gehört wem anders und DesktopAcrylic bleibt matt/solide
    // bis zum ersten echten Klick. Darum nach Activate() per SetForegroundWindow
    // nachhelfen (AttachThreadInput-Trick); nur beim expliziten Öffnen, nie PreWarm.

    /// <summary>Endgültiges Beenden (Tray-Menü „Beenden"): erlaubt das Schließen,
    /// entfernt den Maus-Hook und schließt das Fenster.</summary>
    public void Shutdown()
    {
        _allowClose = true;
        UnhookMouse();
        Close();
    }

    private const int SW_SHOW = 5;

    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(nint hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(nint hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint hWnd, out uint lpdwProcessId);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll")]
    private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

    /// <summary>Stellt echten Win32-Foreground her (D009): Der Tray-Klick gehört uns
    /// (Input-Kontext), trotzdem verweigert Windows manchmal die Aktivierung — dann
    /// bleibt DesktopAcrylic matt, bis erstmals hineingeklickt wird. Kurz an den
    /// Foreground-Thread hängen, Show/SetForeground, sofort wieder lösen.</summary>
    private void EnsureForeground()
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        if (hwnd == nint.Zero || GetForegroundWindow() == hwnd)
        {
            return;
        }

        try
        {
            var fg = GetForegroundWindow();
            var fgThread = fg != nint.Zero ? GetWindowThreadProcessId(fg, out _) : 0;
            var curThread = GetCurrentThreadId();
            var attached = fgThread != 0 && fgThread != curThread
                && AttachThreadInput(curThread, fgThread, true);
            try
            {
                ShowWindow(hwnd, SW_SHOW);
                SetForegroundWindow(hwnd);
            }
            finally
            {
                if (attached)
                {
                    AttachThreadInput(curThread, fgThread, false);
                }
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // Vordergrund ist Kür, nie Absturzgrund — im Zweifel bleibt Glas matt.
        }
    }

    // Klick-daneben-Schließen ohne Fokus-Abhängigkeit: Ein Low-Level-Maus-Hook meldet
    // jeden Klick außerhalb des Popups (Aktivierung kann vom Spiel verweigert sein).
    // Klicks auf die Taskleiste sind ausgenommen — sie gehören dem Tray-Toggle.
    // Single-Instance-App: genau ein Hook-Ziel zur Zeit.
    private const int WH_MOUSE_LL = 14;
    private const int WM_LBUTTONDOWN = 0x0201;
    private const int WM_RBUTTONDOWN = 0x0204;

    private static PopupWindow? _hookTarget;
    private static nint _mouseHook;
    private HookProc? _hookProc;

    private delegate nint HookProc(int code, nint wParam, nint lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct WinRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll")]
    private static extern nint SetWindowsHookEx(int idHook, HookProc lpfn, nint hMod, uint dwThreadId);

    [DllImport("user32.dll")]
    private static extern bool UnhookWindowsHookEx(nint hhk);

    [DllImport("user32.dll")]
    private static extern nint CallNextHookEx(nint hhk, int code, nint wParam, nint lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern nint GetModuleHandle(string? lpModuleName);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(nint hWnd, out WinRect rect);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern nint FindWindow(string? lpClassName, string? lpWindowName);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(nint hWnd, System.Text.StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, nint lParam);

    private delegate bool EnumWindowsProc(nint hWnd, nint lParam);

    private void HookMouse()
    {
        if (_mouseHook != nint.Zero)
        {
            return;
        }

        _hookProc = MouseHookProc;
        _hookTarget = this;
        _mouseHook = SetWindowsHookEx(WH_MOUSE_LL, _hookProc, GetModuleHandle(null), 0);
        if (_mouseHook == nint.Zero)
        {
            _hookTarget = null;
            _hookProc = null;
        }
    }

    private void UnhookMouse()
    {
        if (_mouseHook == nint.Zero)
        {
            return;
        }

        UnhookWindowsHookEx(_mouseHook);
        _mouseHook = nint.Zero;
        _hookTarget = null;
        _hookProc = null;
    }

    private static nint MouseHookProc(int code, nint wParam, nint lParam)
    {
        if (code >= 0 && (wParam == WM_LBUTTONDOWN || wParam == WM_RBUTTONDOWN))
        {
            _hookTarget?._dispatcher.TryEnqueue(() => _hookTarget.DismissOnOutsideClick());
        }

        return CallNextHookEx(_mouseHook, code, wParam, lParam);
    }

    private void DismissOnOutsideClick()
    {
        if (!_shown || _suspendDismiss)
        {
            return;
        }

        try
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            if (hwnd == nint.Zero
                || !GetCursorPos(out var cursor)
                || !GetWindowRect(hwnd, out var rect))
            {
                return;
            }

            if (cursor.X >= rect.Left && cursor.X < rect.Right
                && cursor.Y >= rect.Top && cursor.Y < rect.Bottom)
            {
                return;
            }

            if (IsOnTaskbar(cursor))
            {
                return;
            }

            HidePopup();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // Schließen darf nie crashen; im Zweifel offen bleiben.
        }
    }

    private static bool IsOnTaskbar(PointInt32 point)
    {
        try
        {
            var primary = FindWindow("Shell_TrayWnd", null);
            if (primary != nint.Zero
                && GetWindowRect(primary, out var primaryRect)
                && Contains(primaryRect, point))
            {
                return true;
            }

            var found = false;
            EnumWindows((hWnd, _) =>
            {
                var name = new System.Text.StringBuilder(64);
                if (GetClassName(hWnd, name, name.Capacity) > 0
                    && name.ToString() == "Shell_SecondaryTrayWnd"
                    && GetWindowRect(hWnd, out var secondaryRect)
                    && Contains(secondaryRect, point))
                {
                    found = true;
                    return false;
                }

                return true;
            }, nint.Zero);
            return found;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return false;
        }
    }

    private static bool Contains(WinRect rect, PointInt32 point) =>
        point.X >= rect.Left && point.X < rect.Right
        && point.Y >= rect.Top && point.Y < rect.Bottom;

    private IReadOnlyList<Account> _orderedAccounts = [];

    /// <summary>Konten in Kartenreihenfolge (F007-T2, <see cref="AccountOrder"/>).
    /// Leer ohne Konten (D012-Leere-Regel: keine Karte ohne Konto). Wirft nie.</summary>
    private IReadOnlyList<Account> OrderedAccounts()
    {
        try
        {
            return AccountOrder.Sort(_accounts.Load().Where(a => !string.IsNullOrWhiteSpace(a.Name)));
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return [];
        }
    }

    /// <summary>Lädt den Cache-Eintrag eines Kontos, mit Rückfall auf den alten
    /// Einzelkarten-Eintrag des Providers (Migration ohne Neuabruf, F006-T2/F007-T2).</summary>
    private QuotaSnapshot? LoadAccountCached(Account account)
    {
        var legacy = AccountProviderNames.LegacyCacheKey(account.Provider);
        return _cache.Load(account.Name)
            ?? (account.Name != legacy ? _cache.Load(legacy) : null);
    }

    /// <summary>Alle Konten rendern (F007-T2): je Konto genau eine Karte in Block-
    /// reihenfolge, erstes Konto je Provider auf der statischen Karte (Titel =
    /// Kontoname + Badge), weitere dynamisch. Anbieter ohne Konto bleiben unsichtbar
    /// (D012-Leere-Regel), null Konten → Empty-State.</summary>
    private void UpdateUi(IReadOnlyDictionary<string, QuotaResult> results)
    {
        var now = DateTimeOffset.Now;
        var accounts = _orderedAccounts = OrderedAccounts();
        PruneExtraAccountCards(accounts);
        var firsts = FirstNames(accounts);
        foreach (var account in accounts)
        {
            results.TryGetValue(account.Name, out var live);
            var card = CardDisplayBuilder.Build(account.Name, live, LoadAccountCached(account), now);
            ApplyAccountCard(card, account, firsts[account.Provider] == account.Name);
        }
        UpdateProviderCards(accounts);
        UpdateEmptyState(accounts.Count > 0);
        OrderExtraCards(accounts);
        FitWindowToContent();
    }

    /// <summary>Erster Kontoname je Provider (statische Karte) im aktuellen Durchgang.</summary>
    private static Dictionary<string, string> FirstNames(IReadOnlyList<Account> accounts)
    {
        var firsts = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var account in accounts)
        {
            firsts.TryAdd(account.Provider, account.Name);
        }

        return firsts;
    }

    /// <summary>Versteckt statische Karten der Anbieter ohne Konto (D012).</summary>
    private void UpdateProviderCards(IReadOnlyList<Account> accounts)
    {
        foreach (var provider in AccountProviderNames.All)
        {
            if (!accounts.Any(a => a.Provider == provider))
            {
                StaticBorder(provider).Visibility = Visibility.Collapsed;
            }
        }
    }

    private void UpdateEmptyState(bool hasAccounts)
    {
        EmptyStatePanel.Visibility = hasAccounts ? Visibility.Collapsed : Visibility.Visible;
    }

    private void ApplyAccountCard(CardDisplay card, Account account, bool isFirst)
    {
        if (isFirst)
        {
            var controls = StaticControls(account.Provider);
            controls.Title.Text = card.Provider;
            controls.Badge.Text = AccountProviderNames.Display(account.Provider);
            ApplyStaticCard(card, controls.Border, controls.Panel, controls.Setup, controls.Error, controls.Updated);
            return;
        }

        ApplyExtraAccountCard(card, account);
    }

    private (Border Border, TextBlock Title, TextBlock Badge, StackPanel Panel, StackPanel Setup, TextBlock Error, TextBlock Updated) StaticControls(string provider) =>
        provider switch
        {
            AccountProviders.Claude => (ClaudeCard, ClaudeTitle, ClaudeBadge, CardPanel, SetupPanel, ErrorText, UpdatedText),
            AccountProviders.OpenCodeGo => (GoCard, GoTitle, GoBadge, GoCardPanel, GoSetupPanel, GoErrorText, GoUpdatedText),
            _ => (CodexCard, CodexTitle, CodexBadge, CodexCardPanel, CodexSetupPanel, CodexErrorText, CodexUpdatedText),
        };

    private Border StaticBorder(string provider) => StaticControls(provider).Border;

    /// <summary>Zusatz-Konto ab dem zweiten Konto je Provider (F007-T2): dynamische Karte
    /// mit den Fenstern des Providers, gleichem Prozent-Vokabular („übrig" via
    /// CardDisplayBuilder) und provider-spezifischem Setup-Hinweis.</summary>
    private void ApplyExtraAccountCard(CardDisplay card, Account account)
    {
        var extra = GetOrCreateExtraAccountCard(account);
        extra.Card.Visibility = Visibility.Visible;
        extra.Title.Text = card.Provider;
        extra.Badge.Text = AccountProviderNames.Display(account.Provider);
        if (card.ShowSetup)
        {
            extra.RowsPanel.Visibility = Visibility.Collapsed;
            extra.ErrorText.Visibility = Visibility.Collapsed;
            extra.SetupPanel.Visibility = Visibility.Visible;
            return;
        }

        extra.SetupPanel.Visibility = Visibility.Collapsed;
        extra.RowsPanel.Visibility = Visibility.Visible;

        if (card.ErrorText is not null)
        {
            extra.ErrorText.Text = card.ErrorText;
            extra.ErrorText.Visibility = Visibility.Visible;
        }
        else
        {
            extra.ErrorText.Visibility = Visibility.Collapsed;
        }

        foreach (var row in card.Rows)
        {
            var (label, bar, reset) = extra.Lookup(row.WindowName);
            if (label is null || bar is null || reset is null)
            {
                continue;
            }

            label.Text = row.ValueText;
            bar.Value = row.BarValue;
            reset.Text = row.ResetText;
        }

        if (card.UpdatedText is not null)
        {
            extra.UpdatedText.Text = card.UpdatedText;
        }
    }

    private void ApplyStaticCard(
        CardDisplay card,
        Border border,
        StackPanel cardPanel,
        StackPanel setupPanel,
        TextBlock errorText,
        TextBlock updatedText)
    {
        border.Visibility = Visibility.Visible;
        if (card.ShowSetup)
        {
            cardPanel.Visibility = Visibility.Collapsed;
            errorText.Visibility = Visibility.Collapsed;
            setupPanel.Visibility = Visibility.Visible;
            return;
        }

        setupPanel.Visibility = Visibility.Collapsed;
        cardPanel.Visibility = Visibility.Visible;

        if (card.ErrorText is not null)
        {
            errorText.Text = card.ErrorText;
            errorText.Visibility = Visibility.Visible;
        }
        else
        {
            errorText.Visibility = Visibility.Collapsed;
        }

        foreach (var row in card.Rows)
        {
            var (label, bar, reset) = LookupRowControls(card.Provider, row.WindowName);
            if (label is null || bar is null || reset is null)
            {
                continue;
            }

            label.Text = row.ValueText;
            bar.Value = row.BarValue;
            reset.Text = row.ResetText;
        }

        if (card.UpdatedText is not null)
        {
            updatedText.Text = card.UpdatedText;
        }
    }

    private (TextBlock? Label, ProgressBar? Bar, TextBlock? Reset) LookupRowControls(string provider, string window)
    {
        var account = _orderedAccounts.FirstOrDefault(a => a.Name == provider);
        if (account is null || IsFirstOfProvider(account))
        {
            // Erstes Konto je Provider (bzw. defensiv: unbekannter Schlüssel) nutzt die
            // statische Karte; weitere Konten lösen ihre Controls direkt auf.
            var effective = account?.Provider ?? LegacyProvider(provider);
            if (effective == AccountProviders.Codex)
            {
                return window switch
                {
                    "5 Stunden" => (CodexFiveHourValue, CodexFiveHourBar, CodexFiveHourReset),
                    "Woche" => (CodexWeekValue, CodexWeekBar, CodexWeekReset),
                    _ => (null, null, null),
                };
            }

            if (effective == AccountProviders.OpenCodeGo)
            {
                return window switch
                {
                    "Rolling" => (GoRollingValue, GoRollingBar, GoRollingReset),
                    "Woche" => (GoWeekValue, GoWeekBar, GoWeekReset),
                    "Monat" => (GoMonthValue, GoMonthBar, GoMonthReset),
                    _ => (null, null, null),
                };
            }

            return window switch
            {
                "Session" => (SessionValue, SessionBar, SessionReset),
                "Woche" => (WeekValue, WeekBar, WeekReset),
                _ => (null, null, null),
            };
        }

        if (_extraAccountCards.TryGetValue(provider, out var extra))
        {
            return extra.Lookup(window);
        }

        return (null, null, null);
    }

    private bool IsFirstOfProvider(Account account)
    {
        foreach (var other in _orderedAccounts)
        {
            if (other.Provider == account.Provider)
            {
                return other.Name == account.Name;
            }
        }

        return true;
    }

    private static string LegacyProvider(string name) => name switch
    {
        "OpenCode Go" => AccountProviders.OpenCodeGo,
        "Codex" => AccountProviders.Codex,
        "Claude" => AccountProviders.Claude,
        _ => AccountProviders.Codex,
    };

    /// <summary>Dynamische Zusatz-Karten ab dem zweiten Konto je Provider (F007-T2).
    /// Fenster und Setup-Hinweis je Provider, gleiches Prozent-Vokabular („übrig",
    /// via CardDisplayBuilder), gleiche Fehlertexte wie die statische Karte.</summary>
    private sealed class ExtraAccountCard
    {
        public string Provider = AccountProviders.Codex;
        public Border Card = null!;
        public TextBlock Title = null!;
        public TextBlock Badge = null!;
        public StackPanel RowsPanel = null!;
        public StackPanel SetupPanel = null!;
        public TextBlock ErrorText = null!;
        public TextBlock UpdatedText = null!;
        public readonly Dictionary<string, (TextBlock Label, ProgressBar Bar, TextBlock Reset)> Rows =
            new(StringComparer.Ordinal);

        public (TextBlock? Label, ProgressBar? Bar, TextBlock? Reset) Lookup(string window) =>
            Rows.TryGetValue(window, out var row) ? row : (null, null, null);
    }

    private static IReadOnlyList<string> ExtraWindows(string provider) => provider switch
    {
        AccountProviders.Claude => ["Session", "Woche"],
        AccountProviders.OpenCodeGo => ["Rolling", "Woche", "Monat"],
        _ => ["5 Stunden", "Woche"],
    };

    private static (string Title, string Hint, string Command) ExtraSetup(string provider) => provider switch
    {
        AccountProviders.Claude => ("Kein Claude-Login gefunden.",
            "Melde dich einmalig im Terminal an und hole danach die Stände ab:", "claude login"),
        AccountProviders.OpenCodeGo => ("Kein OpenCode-Go-Key gefunden.",
            "Hinterlege einmalig den API-Key in Pi und hole danach die Stände ab:",
            "~/.pi/agent/auth.json → opencode-go.key"),
        _ => ("Kein Codex-Login gefunden.",
            "Melde dich einmalig im Terminal an und hole danach die Stände ab:", "codex login"),
    };

    private ExtraAccountCard GetOrCreateExtraAccountCard(Account account)
    {
        if (_extraAccountCards.TryGetValue(account.Name, out var existing))
        {
            existing.Card.Visibility = Visibility.Visible;
            return existing;
        }

        var cardStyle = ShellRoot.Resources["GlassCardStyle"] as Style;
        var barStyle = ShellRoot.Resources["RoundedBarStyle"] as Style;
        Style? accentStyle = null;
        try
        {
            if (Application.Current.Resources.TryGetValue("AccentButtonStyle", out var accent))
            {
                accentStyle = accent as Style;
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
        }
        Microsoft.UI.Xaml.Media.Brush? cautionBrush = null;
        try
        {
            if (Application.Current.Resources.TryGetValue("SystemFillColorCautionBrush", out var caution))
            {
                cautionBrush = caution as Microsoft.UI.Xaml.Media.Brush;
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
        }

        var title = new TextBlock { FontSize = 20, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center };
        var badge = new TextBlock { FontSize = 11, Opacity = 0.6 };
        // Karten-Menü wie auf der statischen Codex-Karte (F006-T3): Umbenennen/Entfernen
        // je Konto. Der typisierte Account-Tag kann nicht mit Provider-IDs kollidieren.
        var menuButton = new Button
        {
            Content = "\uE712",
            FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Segoe MDL2 Assets"),
            HorizontalAlignment = HorizontalAlignment.Right,
            Tag = account,
        };
        try
        {
            if (Application.Current.Resources.TryGetValue("SubtleButtonStyle", out var subtle))
            {
                menuButton.Style = subtle as Style;
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
        }
        var renameItem = new MenuFlyoutItem { Text = "Umbenennen", Tag = account };
        renameItem.Click += OnRenameAccountClicked;
        var removeItem = new MenuFlyoutItem { Text = "Entfernen", Tag = account };
        removeItem.Click += OnRemoveAccountClicked;
        var flyout = new MenuFlyout();
        flyout.Items.Add(renameItem);
        flyout.Items.Add(removeItem);
        menuButton.Flyout = flyout;
        var header = new Grid();
        header.Children.Add(title);
        header.Children.Add(menuButton);
        (StackPanel panel, TextBlock value, ProgressBar bar, TextBlock reset) MakeRow(string windowName)
        {
            var valueText = new TextBlock { Text = "–", HorizontalAlignment = HorizontalAlignment.Right };
            var header = new Grid();
            header.Children.Add(new TextBlock { Text = windowName });
            header.Children.Add(valueText);
            var progress = new ProgressBar { Minimum = 0, Maximum = 100 };
            if (barStyle is not null)
            {
                progress.Style = barStyle;
            }
            var resetText = new TextBlock { Text = "", FontSize = 12, Opacity = 0.7 };
            var row = new StackPanel { Spacing = 4 };
            row.Children.Add(header);
            row.Children.Add(progress);
            row.Children.Add(resetText);
            return (row, valueText, progress, resetText);
        }
        var rowsPanel = new StackPanel { Spacing = 12 };
        var rows = new Dictionary<string, (TextBlock Label, ProgressBar Bar, TextBlock Reset)>(StringComparer.Ordinal);
        foreach (var windowName in ExtraWindows(account.Provider))
        {
            var (rowPanel, value, bar, reset) = MakeRow(windowName);
            rowsPanel.Children.Add(rowPanel);
            rows[windowName] = (value, bar, reset);
        }

        var (setupTitle, setupHint, setupCommand) = ExtraSetup(account.Provider);
        var setupPanel = new StackPanel { Spacing = 8, Visibility = Visibility.Collapsed };
        setupPanel.Children.Add(new TextBlock { Text = setupTitle, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });
        setupPanel.Children.Add(new TextBlock { Text = setupHint, TextWrapping = TextWrapping.Wrap, Opacity = 0.85 });
        setupPanel.Children.Add(new TextBlock { Text = setupCommand, FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas"), TextWrapping = TextWrapping.Wrap });
        var setupButton = new Button { Content = "Erneut abrufen" };
        if (accentStyle is not null)
        {
            setupButton.Style = accentStyle;
        }
        setupButton.Click += OnRefreshClicked;
        setupPanel.Children.Add(setupButton);

        var errorText = new TextBlock { Visibility = Visibility.Collapsed, TextWrapping = TextWrapping.Wrap };
        if (cautionBrush is not null)
        {
            errorText.Foreground = cautionBrush;
        }
        var updatedText = new TextBlock { Text = "Noch nie aktualisiert", FontSize = 12, Opacity = 0.7 };

        var body = new StackPanel { Spacing = 12 };
        body.Children.Add(header);
        body.Children.Add(badge);
        body.Children.Add(rowsPanel);
        body.Children.Add(setupPanel);
        body.Children.Add(errorText);
        body.Children.Add(updatedText);
        var card = new Border { Child = body };
        if (cardStyle is not null)
        {
            card.Style = cardStyle;
        }

        var extra = new ExtraAccountCard
        {
            Provider = account.Provider,
            Card = card,
            Title = title,
            Badge = badge,
            RowsPanel = rowsPanel,
            SetupPanel = setupPanel,
            ErrorText = errorText,
            UpdatedText = updatedText,
        };
        foreach (var (windowName, controls) in rows)
        {
            extra.Rows[windowName] = controls;
        }
        _extraAccountCards[account.Name] = extra;
        return extra;
    }

    private void RenderCached()
    {
        var now = DateTimeOffset.Now;
        var accounts = _orderedAccounts = OrderedAccounts();
        PruneExtraAccountCards(accounts);
        var firsts = FirstNames(accounts);
        foreach (var account in accounts)
        {
            var snapshot = LoadAccountCached(account);
            if (snapshot is not null)
            {
                ApplyAccountCard(
                    CardDisplayBuilder.Build(account.Name, null, snapshot, now),
                    account,
                    firsts[account.Provider] == account.Name);
            }
        }
        UpdateProviderCards(accounts);
        UpdateEmptyState(accounts.Count > 0);
        OrderExtraCards(accounts);
        FitWindowToContent();
    }

    /// <summary>Fensterhöhe folgt dem Inhalt (F008): CardsPanel messen, Höhe = Inhalt
    /// + Chrome-Extra, Cap = min(860, Arbeitshöhe − 32), Breite fix 380. Danach unten-
    /// rechts andocken. Schlägt etwas fehl, bleibt die aktuelle Größe (nie Absturz).</summary>
    private void FitWindowToContent()
    {
        try
        {
            CardsPanel.Measure(new Windows.Foundation.Size(PopupSizing.FixedWidth - 40, double.PositiveInfinity));
            var content = CardsPanel.DesiredSize.Height + ChromeExtra;
            var area = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary).WorkArea;
            var capped = PopupSizing.Fit(content, PopupSizing.MaxHeight(area.Height));
            if (Math.Abs(capped - AppWindow.Size.Height) > 1)
            {
                AppWindow.Resize(new SizeInt32((int)PopupSizing.FixedWidth, capped));
            }
            MoveToBottomRight();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
        }
    }

    /// <summary>Shell-Chrome um den Karteninhalt (Rundung + ScrollViewer-Rahmen + Reserve).</summary>
    private const double ChromeExtra = 24;

    /// <summary>Zeigt einen ContentDialog mit genug Platz (F008-Fix): Das atmende Popup
    /// ist ohne/vielen wenigen Konten kleiner als der Dialog braucht — temporär auf
    /// DialogMinHeight wachsen (gecappt, nur wachsen), Klick-daneben-Schließen solange
    /// sperren, danach via FitWindowToContent() wieder schrumpfen. Wirft nie.</summary>
    private async Task<ContentDialogResult> ShowDialogAsync(ContentDialog dialog)
    {
        var wasSuspended = _suspendDismiss;
        _suspendDismiss = true;
        try
        {
            try
            {
                var area = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary).WorkArea;
                var want = PopupSizing.DialogHeight(AppWindow.Size.Height, area.Height);
                if (want > AppWindow.Size.Height)
                {
                    AppWindow.Resize(new SizeInt32((int)PopupSizing.FixedWidth, want));
                    MoveToBottomRight();
                }
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
            }
            return await dialog.ShowAsync();
        }
        finally
        {
            _suspendDismiss = wasSuspended;
            FitWindowToContent();
        }
    }

    private async void OnRefreshClicked(object sender, RoutedEventArgs e)
    {
        Render(await _refresh.RefreshAllAsync());
    }

    /// <summary>Menü „App aktualisieren …" (F009-T2): GitHub Releases gegen die laufende
    /// Version prüfen. Fund → Download + Rückfrage → Setup-Start + Beenden; aktuell oder
    /// Fehler → Hinweis-Dialog. Öffnet zuerst das Popup (Kontext + Sichtbarkeit). Wirft nie.</summary>
    public async Task CheckForAppUpdateAsync()
    {
        try
        {
            if (_dispatcher.HasThreadAccess)
            {
                await CheckForAppUpdateCoreAsync();
            }
            else
            {
                var done = new TaskCompletionSource();
                _dispatcher.TryEnqueue(async () =>
                {
                    try
                    {
                        await CheckForAppUpdateCoreAsync();
                        done.SetResult();
                    }
                    catch (Exception ex) when (ex is not OutOfMemoryException)
                    {
                        done.SetException(ex);
                    }
                });
                await done.Task;
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
        }
    }

    private async Task CheckForAppUpdateCoreAsync()
    {
        ShowIfHiddenCore();
        var current = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(0, 0, 0);
        var result = await new UpdateChecker(_http).CheckAsync(current);
        switch (result)
        {
            case UpdateCheckResult.Available available:
                await OfferAppUpdateAsync(available.Release);
                break;
            case UpdateCheckResult.Current:
                await ShowUpdateNoteAsync($"Du bist aktuell ({AppVersion.Format(current)}).", "App aktualisieren");
                break;
            case UpdateCheckResult.Unavailable unavailable:
                await ShowUpdateNoteAsync(unavailable.Reason, "App aktualisieren");
                break;
        }
    }

    private async Task OfferAppUpdateAsync(ReleaseInfo release)
    {
        var dialog = new ContentDialog
        {
            Title = $"Version {release.Version} verfügbar",
            Content = $"BirdyCreditStatus {release.Version} steht bereit (du hast {AppVersion.Format(System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version)}). Jetzt herunterladen und installieren?",
            PrimaryButtonText = "Jetzt installieren",
            CloseButtonText = "Später",
            XamlRoot = Content.XamlRoot,
        };
        if (await ShowDialogAsync(dialog) != ContentDialogResult.Primary)
        {
            return;
        }

        string setupPath;
        try
        {
            setupPath = Path.Combine(Path.GetTempPath(), $"birdy-creditStatus-Setup-{release.Version}.exe");
            await new UpdateChecker(_http).DownloadAsync(release.DownloadUrl, setupPath);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            await ShowUpdateNoteAsync("Download fehlgeschlagen — bitte später erneut versuchen.", "App aktualisieren");
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(setupPath) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            await ShowUpdateNoteAsync("Setup konnte nicht gestartet werden.", "App aktualisieren");
            return;
        }

        (Application.Current as App)?.ShutdownApp();
    }

    private async Task ShowUpdateNoteAsync(string text, string title)
    {
        try
        {
            await ShowDialogAsync(new ContentDialog
            {
                Title = title,
                Content = text,
                CloseButtonText = "OK",
                XamlRoot = Content.XamlRoot,
            });
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
        }
    }

    /// <summary>Entfernt dynamische Zusatz-Karten, deren Konto nicht mehr erstes Konto
    /// des Providers ist oder gar nicht mehr in der Liste steht (F006-T3/F007-T2:
    /// nach Entfernen/Umbenennen, sonst blieben verwaiste Karten sichtbar).</summary>
    private void PruneExtraAccountCards(IReadOnlyList<Account> accounts)
    {
        try
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var keep = new HashSet<string>(StringComparer.Ordinal);
            foreach (var account in accounts)
            {
                if (seen.Add(account.Provider))
                {
                    continue;
                }

                keep.Add(account.Name);
            }
            foreach (var key in _extraAccountCards.Keys.Where(k => !keep.Contains(k)).ToList())
            {
                if (_extraAccountCards.Remove(key, out var card))
                {
                    CardsPanel.Children.Remove(card.Card);
                }
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // Aufräumen darf nie crashen; im Zweifel bleibt eine stale Karte bis zum Refresh.
        }
    }

    /// <summary>Sortiert Zusatz-Karten in ihre Provider-Blöcke (F007-T2): je Block direkt
    /// hinter die statische Karte, innen Anlagereihenfolge. Karte ist Kür, nie Absturzgrund.</summary>
    private void OrderExtraCards(IReadOnlyList<Account> accounts)
    {
        try
        {
            foreach (var provider in AccountProviderNames.All)
            {
                var anchor = StaticBorder(provider);
                var index = CardsPanel.Children.IndexOf(anchor);
                if (index < 0)
                {
                    continue;
                }
                index++;
                var seenFirst = false;
                foreach (var account in accounts)
                {
                    if (account.Provider != provider)
                    {
                        continue;
                    }
                    if (!seenFirst)
                    {
                        seenFirst = true;
                        continue;
                    }
                    if (!_extraAccountCards.TryGetValue(account.Name, out var extra))
                    {
                        continue;
                    }
                    CardsPanel.Children.Remove(extra.Card);
                    if (index > CardsPanel.Children.Count)
                    {
                        index = CardsPanel.Children.Count;
                    }
                    CardsPanel.Children.Insert(index, extra.Card);
                    index++;
                }
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // Karte ist Kür, nie Absturzgrund — im Zweifel statisch weiter.
        }
    }

    /// <summary>Erzeugt einen Konto-Adapter über die Fabrik, wirft nie (Fallback: n/a-Adapter).</summary>
    private IQuotaAdapter CreateAdapter(Account account)
    {
        try
        {
            return _adapterFactory(account);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return new FakeCodexAdapter { Provider = account.Name, Mode = FakeMode.NotAvailable };
        }
    }

    /// <summary>Löst ein Menü-Tag auf den Kontonamen auf (F007-T3): Extras tragen den
    /// Account-Datensatz, statische Karten die Provider-Kennung (→ jeweils erstes Konto).</summary>
    private string? ResolveAccountTarget(object? tag) =>
        AccountMenuTarget.Resolve(tag, OrderedAccounts());

    private IReadOnlyList<string> ExistingAccountNames() =>
        _accounts.Load()
            .Where(a => !string.IsNullOrWhiteSpace(a.Name))
            .Select(a => a.Name)
            .ToList();

    /// <summary>Dialog „Konto hinzufügen" (F007-T3): Anbieter-Dropdown + Name + Auth-Datei
    /// (Default je Provider, Dateidialog per Durchsuchen). Validierung ohne Absturz;
    /// gültig → Karte nach Refresh ohne Neustart, ungültig → Meldung im Dialog, keine Karte.</summary>
    private async void OnAddAccountClicked(object sender, RoutedEventArgs e)
    {
        var providerBox = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch };
        var codexIndex = 0;
        foreach (var (id, index) in AccountProviderNames.All.Select((id, i) => (id, i)))
        {
            providerBox.Items.Add(new ComboBoxItem { Content = AccountProviderNames.Display(id), Tag = id });
            if (id == AccountProviders.Codex)
            {
                codexIndex = index;
            }
        }
        providerBox.SelectedIndex = codexIndex;
        var nameBox = new TextBox { PlaceholderText = "z. B. Codex privat" };
        var pathBox = new TextBox
        {
            Text = AccountProviderPaths.DefaultAuthPath(AccountProviders.Codex),
            PlaceholderText = AccountProviderPaths.DefaultAuthPath(AccountProviders.Codex),
        };
        providerBox.SelectionChanged += (_, _) =>
        {
            var def = AccountProviderPaths.DefaultAuthPath(SelectedProvider(providerBox));
            pathBox.Text = def;
            pathBox.PlaceholderText = def;
        };
        var browseButton = new Button { Content = "Durchsuchen …" };
        browseButton.Click += async (_, _) =>
        {
            var picked = await PickAuthFileAsync(SelectedProvider(providerBox));
            if (picked is not null)
            {
                pathBox.Text = picked;
            }
        };
        var errorText = new TextBlock { Visibility = Visibility.Collapsed, TextWrapping = TextWrapping.Wrap };
        try
        {
            if (Application.Current.Resources.TryGetValue("SystemFillColorCautionBrush", out var caution))
            {
                errorText.Foreground = caution as Microsoft.UI.Xaml.Media.Brush;
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
        }
        var panel = new StackPanel { Spacing = 8 };
        var detectButton = new Button { Content = "Automatisch erkennen …", HorizontalAlignment = HorizontalAlignment.Stretch };
        var foundLabel = new TextBlock { Text = "Gefundene Logins", Visibility = Visibility.Collapsed };
        var foundBox = new ComboBox
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Visibility = Visibility.Collapsed,
            PlaceholderText = "Gefundenes Login wählen …",
        };
        foundBox.SelectionChanged += (_, _) =>
        {
            if ((foundBox.SelectedItem as ComboBoxItem)?.Tag is DetectedAuth pick)
            {
                SelectProvider(providerBox, pick.Provider);
                pathBox.Text = pick.Path;
                errorText.Visibility = Visibility.Collapsed;
            }
        };
        detectButton.Click += (_, _) =>
        {
            // F010-T2: nur die 3 Defaults per Marker (Core-Naht, nie Throw);
            // Eingerichtete Pfade filtert Detect bereits raus.
            var configured = _accounts.Load()
                .Where(a => !string.IsNullOrWhiteSpace(a.AuthFilePath))
                .Select(a => a.AuthFilePath)
                .ToList();
            var found = DefaultAuthDiscovery.Detect(configured);
            if (found.Count == 0)
            {
                foundLabel.Visibility = Visibility.Collapsed;
                foundBox.Visibility = Visibility.Collapsed;
                errorText.Text = "Keine Standard-Logins gefunden — bitte Anbieter und Datei manuell wählen.";
                errorText.Visibility = Visibility.Visible;
                return;
            }
            errorText.Visibility = Visibility.Collapsed;
            if (found.Count == 1)
            {
                foundLabel.Visibility = Visibility.Collapsed;
                foundBox.Visibility = Visibility.Collapsed;
                SelectProvider(providerBox, found[0].Provider);
                pathBox.Text = found[0].Path;
                return;
            }
            foundBox.Items.Clear();
            foreach (var candidate in found)
            {
                foundBox.Items.Add(new ComboBoxItem
                {
                    Content = $"{candidate.DisplayName} — {candidate.Path}",
                    Tag = candidate,
                });
            }
            foundBox.SelectedIndex = -1;
            foundLabel.Visibility = Visibility.Visible;
            foundBox.Visibility = Visibility.Visible;
        };
        panel.Children.Add(detectButton);
        panel.Children.Add(foundLabel);
        panel.Children.Add(foundBox);
        panel.Children.Add(new TextBlock { Text = "Anbieter" });
        panel.Children.Add(providerBox);
        panel.Children.Add(new TextBlock { Text = "Name" });
        panel.Children.Add(nameBox);
        panel.Children.Add(new TextBlock { Text = "Auth-Datei" });
        panel.Children.Add(pathBox);
        panel.Children.Add(browseButton);
        panel.Children.Add(errorText);

        var dialog = new ContentDialog
        {
            Title = "Konto hinzufügen",
            Content = new ScrollViewer
            {
                Content = panel,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollMode = ScrollMode.Disabled,
            },
            PrimaryButtonText = "Hinzufügen",
            CloseButtonText = "Abbrechen",
            XamlRoot = Content.XamlRoot,
        };
        var confirmed = false;
        string provider = AccountProviders.Codex;
        string name = string.Empty;
        string path = string.Empty;
        dialog.PrimaryButtonClick += (_, args) =>
        {
            provider = SelectedProvider(providerBox);
            name = nameBox.Text.Trim();
            path = pathBox.Text.Trim();
            var problem = AccountValidator.ValidateName(name, ExistingAccountNames())
                ?? AccountValidator.ValidateAuthFile(provider, path);
            if (problem is not null)
            {
                args.Cancel = true;
                errorText.Text = problem;
                errorText.Visibility = Visibility.Visible;
                return;
            }

            if (!_accounts.Add(new Account(provider, name, path)))
            {
                args.Cancel = true;
                errorText.Text = $"„{name}“ konnte nicht gespeichert werden — bitte erneut versuchen.";
                errorText.Visibility = Visibility.Visible;
                return;
            }

            confirmed = true;
        };
        await ShowDialogAsync(dialog);
        if (!confirmed)
        {
            return;
        }

        _refresh.UpsertAdapter(name, CreateAdapter(new Account(provider, name, path)));
        Render(await _refresh.RefreshAllAsync());
    }

    private static string SelectedProvider(ComboBox box) =>
        (box.SelectedItem as ComboBoxItem)?.Tag as string is { } tag && AccountProviders.IsKnown(tag)
            ? tag
            : AccountProviders.Codex;

    /// <summary>Stellt die Anbieter-Dropdown auf den erkannten Provider (F010-T2).</summary>
    private static void SelectProvider(ComboBox box, string provider)
    {
        for (var i = 0; i < box.Items.Count; i++)
        {
            if ((box.Items[i] as ComboBoxItem)?.Tag as string == provider)
            {
                box.SelectedIndex = i;
                return;
            }
        }
    }

    /// <summary>Karten-Menü „Umbenennen" (F006-T3): wirkt sofort (Liste + Adapter +
    /// Cache wandern mit), Datei und übrige Karten unberührt.</summary>
    private async void OnRenameAccountClicked(object sender, RoutedEventArgs e)
    {
        var oldName = ResolveAccountTarget((sender as MenuFlyoutItem)?.Tag);
        if (string.IsNullOrWhiteSpace(oldName))
        {
            return;
        }

        var account = _accounts.Load().FirstOrDefault(a => a.Name == oldName);
        if (account is null)
        {
            return;
        }

        var nameBox = new TextBox { Text = oldName };
        var errorText = new TextBlock { Visibility = Visibility.Collapsed, TextWrapping = TextWrapping.Wrap };
        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(new TextBlock { Text = "Neuer Name" });
        panel.Children.Add(nameBox);
        panel.Children.Add(errorText);

        var dialog = new ContentDialog
        {
            Title = $"„{oldName}“ umbenennen",
            Content = panel,
            PrimaryButtonText = "Umbenennen",
            CloseButtonText = "Abbrechen",
            XamlRoot = Content.XamlRoot,
        };
        var confirmed = false;
        string newName = string.Empty;
        dialog.PrimaryButtonClick += (_, args) =>
        {
            newName = nameBox.Text.Trim();
            if (newName == oldName)
            {
                // Unverändert → Dialog schließen, nichts tun.
                return;
            }

            var others = ExistingAccountNames().Where(n => n != oldName);
            var problem = AccountValidator.ValidateName(newName, others);
            if (problem is not null)
            {
                args.Cancel = true;
                errorText.Text = problem;
                errorText.Visibility = Visibility.Visible;
                return;
            }

            if (!_accounts.Rename(oldName, newName))
            {
                args.Cancel = true;
                errorText.Text = $"„{oldName}“ konnte nicht umbenannt werden — bitte erneut versuchen.";
                errorText.Visibility = Visibility.Visible;
                return;
            }

            confirmed = true;
        };
        await ShowDialogAsync(dialog);
        if (!confirmed)
        {
            return;
        }

        // Adapter wandert mit neuem Kontonamen (Provider-Schlüssel), Cache-Eintrag ebenso.
        _refresh.RemoveAdapter(oldName);
        _refresh.UpsertAdapter(newName, CreateAdapter(account with { Name = newName }));
        _cache.Migrate(oldName, newName);
        _cache.Remove(oldName);
        Render(await _refresh.RefreshAllAsync());
    }

    /// <summary>Karten-Menü „Entfernen" (F006-T3): löscht nur Listen- und Cache-Eintrag,
    /// nie die Auth-Datei selbst. Wirkt sofort (Abruf + Render, D010-Pfad).</summary>
    private async void OnRemoveAccountClicked(object sender, RoutedEventArgs e)
    {
        var name = ResolveAccountTarget((sender as MenuFlyoutItem)?.Tag);
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        // Fallback-Setup-Karte („Codex" ohne Listeneintrag) hat nichts zu entfernen.
        if (!_accounts.Load().Any(a => a.Name == name))
        {
            return;
        }

        var dialog = new ContentDialog
        {
            Title = $"„{name}“ entfernen?",
            Content = "Das Konto wird nur aus der Liste entfernt (inklusive zwischengespeicherter Stände). "
                + "Die Auth-Datei bleibt auf der Platte erhalten.",
            PrimaryButtonText = "Entfernen",
            CloseButtonText = "Abbrechen",
            XamlRoot = Content.XamlRoot,
        };
        if (await ShowDialogAsync(dialog) != ContentDialogResult.Primary)
        {
            return;
        }

        if (!_accounts.Remove(name))
        {
            await ShowUpdateNoteAsync(
                $"„{name}“ konnte nicht entfernt werden — bitte Dateirechte prüfen und erneut versuchen.",
                "Konto entfernen");
            return;
        }
        _refresh.RemoveAdapter(name);
        _cache.Remove(name);
        Render(await _refresh.RefreshAllAsync());
    }

    private bool _suspendDismiss;

    /// <summary>Auth-Datei wählen (F007-T3): genau EIN Dateidialog mit JSON-Filter,
    /// Start und Titel je Provider. Gibt null bei Abbruch/Fehler — nie Throw. Hält das Popup
    /// offen, solange der Dialog oben liegt (sonst würde Deactivated/Maus-Hook es
    /// schließen). Bewusst Win32 (comdlg32) statt WinRT-Picker: dieser stirbt
    /// unpackaged beim Bestätigen mit E_FAIL (0x80004005, Abnahme 2026-09-20) —
    /// und gilt auch für die installierte (unpackaged) Version.</summary>
    private Task<string?> PickAuthFileAsync(string provider)
    {
        var wasSuspended = _suspendDismiss;
        _suspendDismiss = true;
        try
        {
            return Task.FromResult(PickAuthFileWin32(provider));
        }
        finally
        {
            _suspendDismiss = wasSuspended;
        }
    }

    private const int OFN_EXPLORER = 0x00080000;
    private const int OFN_NOCHANGEDIR = 0x00000008;
    private const int OFN_FILEMUSTEXIST = 0x00001000;
    private const int OFN_PATHMUSTEXIST = 0x00000800;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct OpenFileName
    {
        public int lStructSize;
        public nint hwndOwner;
        public nint hInstance;
        [MarshalAs(UnmanagedType.LPWStr)] public string lpstrFilter;
        [MarshalAs(UnmanagedType.LPWStr)] public string? lpstrCustomFilter;
        public int nMaxCustFilter;
        public int nFilterIndex;
        [MarshalAs(UnmanagedType.LPWStr)] public string lpstrFile;
        public int nMaxFile;
        [MarshalAs(UnmanagedType.LPWStr)] public string? lpstrFileTitle;
        public int nMaxFileTitle;
        [MarshalAs(UnmanagedType.LPWStr)] public string? lpstrInitialDir;
        [MarshalAs(UnmanagedType.LPWStr)] public string? lpstrTitle;
        public int Flags;
        public short nFileOffset;
        public short nFileExtension;
        [MarshalAs(UnmanagedType.LPWStr)] public string? lpstrDefExt;
        public nint lCustData;
        public nint lpfnHook;
        [MarshalAs(UnmanagedType.LPWStr)] public string? lpTemplateName;
        public nint pvReserved;
        public int dwReserved;
        public int FlagsEx;
    }

    [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetOpenFileName(ref OpenFileName ofn);

    /// <summary>Win32-Dateiwahl (F006-T3, einziger Dialog): Explorer-Dialog mit
    /// JSON-Filter, Startverzeichnis und Titel je Provider (F007-T3).</summary>
    private string? PickAuthFileWin32(string provider)
    {
        try
        {
            var initialDir = AccountProviderPaths.InitialDirectory(provider);
            var ofn = new OpenFileName
            {
                lpstrFilter = "JSON-Dateien (*.json)\0*.json\0Alle Dateien (*.*)\0*.*\0\0",
                nFilterIndex = 1,
                lpstrFile = new string('\0', 512),
                nMaxFile = 512,
                lpstrInitialDir = Directory.Exists(initialDir) ? initialDir : null,
                lpstrTitle = AccountProviderPaths.FileDialogTitle(provider),
                lpstrDefExt = "json",
                Flags = OFN_EXPLORER | OFN_NOCHANGEDIR | OFN_FILEMUSTEXIST | OFN_PATHMUSTEXIST,
            };
            ofn.lStructSize = Marshal.SizeOf<OpenFileName>();
            ofn.hwndOwner = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var ok = GetOpenFileName(ref ofn);
            var path = ok ? ofn.lpstrFile.TrimEnd('\0') : null;
            return string.IsNullOrWhiteSpace(path) ? null : path;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return null;
        }
    }

    private void MoveToBottomRight()
    {
        // User-Vorgabe: Das Popup öffnet IMMER unten rechts auf dem Hauptbildschirm
        // (DisplayAreaFallback.Primary), nie am Klick-Monitor.
        // PopupPlacement.PickWorkArea bleibt als getesteter Helfer für später erhalten.
        try
        {
            var primary = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary).WorkArea;
            var work = new ScreenArea(primary.X, primary.Y, primary.Width, primary.Height);
            var (x, y) = PopupPlacement.BottomRight(work, AppWindow.Size.Width, AppWindow.Size.Height);
            AppWindow.Move(new PointInt32(x, y));
            return;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // Lieber falsch positioniert als abgestürzt (vgl. birdy-crash.log, F002-Fix).
        }

        var area = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary);
        var workArea = area.WorkArea;
        var (fallbackX, fallbackY) = PopupPlacement.BottomRight(
            new ScreenArea(workArea.X, workArea.Y, workArea.Width, workArea.Height),
            AppWindow.Size.Width,
            AppWindow.Size.Height);
        AppWindow.Move(new PointInt32(fallbackX, fallbackY));
    }

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out PointInt32 point);
}
