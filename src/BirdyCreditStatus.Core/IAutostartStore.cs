namespace BirdyCreditStatus.Core;

/// <summary>Einzige Autostart-Naht (D011): idempotent setzen/löschen,
/// fehlender Key = aus, Fehler → kein Throw (der Key ist Wahrheit).</summary>
public interface IAutostartStore
{
    bool IsEnabled();

    void SetEnabled(bool enabled);
}
