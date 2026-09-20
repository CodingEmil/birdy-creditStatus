using Microsoft.UI.Dispatching;

namespace BirdyCreditStatus;

/// <summary>Single-Instance per benanntem Mutex (F001-T3): Der Zweitstart
/// signalisiert der laufenden Instanz via Event und beendet sich,
/// ohne ein zweites Tray-Icon zu erzeugen. Lebensdauer = Prozess.</summary>
public sealed class SingleInstance : IDisposable
{
    public const string MutexName = "Local\\BirdyCreditStatus-single-instance";
    public const string ShowEventName = "Local\\BirdyCreditStatus-show-popup";

    private readonly Mutex _mutex;
    private readonly EventWaitHandle _showEvent;
    private readonly CancellationTokenSource _cts = new();
    private bool _disposed;

    private SingleInstance(Mutex mutex, EventWaitHandle showEvent)
    {
        _mutex = mutex;
        _showEvent = showEvent;
    }

    public static bool TryAcquireFirst(out SingleInstance? first)
    {
        var mutex = new Mutex(false, MutexName);
        bool owned;
        try
        {
            owned = mutex.WaitOne(0);
        }
        catch (AbandonedMutexException)
        {
            // Vorinstanz abgestürzt — wir übernehmen den Mutex.
            owned = true;
        }

        if (!owned)
        {
            mutex.Dispose();
            first = null;
            return false;
        }

        EventWaitHandle showEvent;
        try
        {
            showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);
        }
        catch
        {
            mutex.Dispose();
            first = null;
            return false;
        }

        first = new SingleInstance(mutex, showEvent);
        return true;
    }

    public static void SignalFirst()
    {
        // Die Erstinstanz erzeugt Mutex und Event unmittelbar nacheinander;
        // kurze Wiederholung schließt die Lücke beim parallelen Start.
        for (var attempt = 0; attempt < 10; attempt++)
        {
            try
            {
                using var showEvent = EventWaitHandle.OpenExisting(ShowEventName);
                showEvent.Set();
                return;
            }
            catch (WaitHandleCannotBeOpenedException)
            {
                Thread.Sleep(100);
            }
            catch
            {
                return;
            }
        }
    }

    /// <summary>Lauscht auf Zweitstarts und ruft <paramref name="activated"/> auf dem UI-Thread.</summary>
    public void Watch(DispatcherQueue dispatcher, Action activated)
    {
        var token = _cts.Token;
        Task.Run(() =>
        {
            while (!token.IsCancellationRequested)
            {
                bool signaled;
                try
                {
                    signaled = _showEvent.WaitOne(TimeSpan.FromMilliseconds(500));
                }
                catch (ObjectDisposedException)
                {
                    return;
                }

                if (signaled && !token.IsCancellationRequested)
                {
                    dispatcher.TryEnqueue(() => activated());
                }
            }
        }, token);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _cts.Cancel();
        _showEvent.Dispose();
        try
        {
            _mutex.ReleaseMutex();
        }
        catch (ApplicationException)
        {
        }
        _mutex.Dispose();
        _cts.Dispose();
    }
}
