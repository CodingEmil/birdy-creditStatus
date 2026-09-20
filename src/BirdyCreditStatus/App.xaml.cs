using Microsoft.UI.Xaml;

namespace BirdyCreditStatus;

public sealed partial class App : Application
{
    private MainWindow? _host;
    private SingleInstance? _first;

    public App()
    {
        InitializeComponent();
        UnhandledException += (_, e) => LogCrash("UI", e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => LogCrash("Domain", e.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            LogCrash("Task", e.Exception);
            e.SetObserved();
        };
    }

    private static void LogCrash(string kind, Exception? ex)
    {
        try
        {
            File.AppendAllText(
                Path.Combine(Path.GetTempPath(), "birdy-crash.log"),
                $"{DateTimeOffset.Now:HH:mm:ss} {kind}: {ex}\n");
        }
        catch
        {
        }
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        // Zweitstart: vorhandene Instanz aktivieren, kein zweites Tray-Icon.
        // Danach sofort beenden — ohne Exit liefe die Instanz fensterlos weiter.
        if (!SingleInstance.TryAcquireFirst(out var first))
        {
            SingleInstance.SignalFirst();
            Exit();
            return;
        }

        _first = first;
        _host = new MainWindow();
        _host.Activate();
        _host.HideHost();
        _first?.Watch(Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread(), () => _host.ShowPopup());
    }

    /// <summary>Endgültiges Beenden per Tray-Menü: Fenster schließen (Close erlaubt),
    /// Mutex/Event freigeben, dann Exit. Watchdog als Zombie-Garantie — ein hängender
    /// Prozess blockiert sonst Rebuild und Neustart.</summary>
    public void ShutdownApp()
    {
        try
        {
            _host?.Shutdown();
            _first?.Dispose();
            _first = null;
        }
        catch (Exception ex)
        {
            LogCrash("Exit", ex);
        }

        _ = Task.Delay(5000).ContinueWith(
            _ => Environment.Exit(0), TaskScheduler.Default);
        Exit();
    }
}
