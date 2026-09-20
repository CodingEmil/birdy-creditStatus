using System.Runtime.InteropServices;
using WinRT.Interop;

namespace BirdyCreditStatus;

/// <summary>Flyout-Stil fürs Popup. Alles läuft auf dem UI-Thread
/// (Aufrufer marshallen via DispatcherQueue).</summary>
internal static class FlyoutStyle
{
    private const int GWL_EXSTYLE = -20;

    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_APPWINDOW = 0x00040000;
    private const int WS_EX_WINDOWEDGE = 0x00000100;

    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_FRAMECHANGED = 0x0020;

    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int DWMWCP_ROUND = 2;
    private const int DWMWA_BORDER_COLOR = 34;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(nint hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowLong(nint hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(nint hWnd, nint hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(nint hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);

    /// <summary>Flyout-Chrome (D008): Toolwindow-Bit (kein Taskleisten-Eintrag),
    /// runde Ecken (systemseitiges Maximum) und dunkle Rahmenfarbe. Stil-Bits
    /// (Caption/Border) werden bewusst NICHT angefasst — AppWindow verwaltet sie
    /// selbst und stellt gestrippte Bits kommentarlos wieder her (Tauziehen).
    /// Der unsichtbare Rahmen kommt aus SetBorderAndTitleBar(true, false) plus
    /// Rahmenfarbe statt aus rohem Strippen.</summary>
    public static bool Apply(Microsoft.UI.Xaml.Window window)
    {
        var hwnd = WindowNative.GetWindowHandle(window);
        if (hwnd == nint.Zero)
        {
            return false;
        }

        var exStyle = GetWindowLong(hwnd, GWL_EXSTYLE) | WS_EX_TOOLWINDOW;
        exStyle &= ~WS_EX_APPWINDOW;
        exStyle &= ~WS_EX_WINDOWEDGE;
        SetWindowLong(hwnd, GWL_EXSTYLE, exStyle);
        SetWindowPos(hwnd, nint.Zero, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_FRAMECHANGED);

        var preference = DWMWCP_ROUND;
        DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref preference, sizeof(int));
        // Dunkler 1px-Rahmen (D008): DWM-Default wäre weiß im Light-System-Theme
        // ums fest dunkle Popup (COLORREF 0x00BBGGRR).
        var border = 0x002B2B2B;
        DwmSetWindowAttribute(hwnd, DWMWA_BORDER_COLOR, ref border, sizeof(int));
        return true;
    }

    /// <summary>Hält die DWM-Rahmenfarbe dunkel — nach jedem FRAMECHANGED erneut
    /// aufrufen, sonst fällt sie auf den System-Default zurück.</summary>
    public static bool RefreshChrome(Microsoft.UI.Xaml.Window window)
    {
        var hwnd = WindowNative.GetWindowHandle(window);
        if (hwnd == nint.Zero)
        {
            return false;
        }

        var border = 0x002B2B2B;
        DwmSetWindowAttribute(hwnd, DWMWA_BORDER_COLOR, ref border, sizeof(int));
        return true;
    }
}
