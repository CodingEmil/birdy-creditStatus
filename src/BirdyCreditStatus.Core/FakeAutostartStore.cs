namespace BirdyCreditStatus.Core;

/// <summary>In-Memory-Fake der Autostart-Naht für Tests (keine echte Registry).
/// <see cref="FailWrites"/> simuliert eine nicht beschreibbare Registry:
/// Schreiben wird still ignoriert, nie geworfen (D011).</summary>
public sealed class FakeAutostartStore : IAutostartStore
{
    private bool _enabled;

    /// <summary>Wenn true, werden Schreiben still ignoriert (Fehlerfall ohne Throw).</summary>
    public bool FailWrites { get; set; }

    public bool IsEnabled() => _enabled;

    public void SetEnabled(bool enabled)
    {
        if (FailWrites)
        {
            return;
        }

        _enabled = enabled;
    }
}
