using Microsoft.Win32;

namespace BirdyCreditStatus.Core;

/// <summary>Registry-Impl der Autostart-Naht (D011):
/// <c>HKCU\Software\Microsoft\Windows\CurrentVersion\Run</c>, der Wert zeigt auf die
/// installierte exe. Idempotent, fehlender Key = aus, Registry-Fehler → kein Throw
/// (Menü bleibt bedienbar, Popup/Karten unberührt).</summary>
public sealed class RegistryAutostartStore : IAutostartStore
{
    /// <summary>Wertname im Run-Key — teilen sich Installer-Checkbox (T2) und Tray-Haken.</summary>
    public const string ValueName = "birdy-creditStatus";

    private const string RunSubKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public bool IsEnabled()
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunSubKey, writable: false);
            return key?.GetValue(ValueName) is not null;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return false;
        }
    }

    public void SetEnabled(bool enabled)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        try
        {
            if (enabled)
            {
                var exePath = Environment.ProcessPath;
                if (string.IsNullOrEmpty(exePath))
                {
                    return;
                }

                using var key = Registry.CurrentUser.OpenSubKey(RunSubKey, writable: true);
                key?.SetValue(ValueName, $"\"{exePath}\"");
            }
            else
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunSubKey, writable: true);
                key?.DeleteValue(ValueName, throwOnMissingValue: false);
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // D011: Registry-Fehler (z. B. nicht beschreibbar) → kein Throw.
        }
    }
}
