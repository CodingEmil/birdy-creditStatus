namespace BirdyCreditStatus.Core;

/// <summary>Höhen-Rechenkern für das atmende Popup (F008): kein Minimum, Cap aus der
/// Arbeitshöhe. Fenster-Interaktion (Messen/Resize) bleibt im Code-Behind.</summary>
public static class PopupSizing
{
    /// <summary>Bevorzugte Fensterhöhe (F007-Fenster).</summary>
    public const double PreferredMaxHeight = 860;

    /// <summary>Fensterbreite (fix).</summary>
    public const double FixedWidth = 380;

    /// <summary>Abstand zur Bildschirmkante im Cap.</summary>
    public const double EdgeMargin = 32;

    /// <summary>Not-Cap bei winziger Arbeitshöhe (Fenster bleibt bedienbar).</summary>
    public const double AbsoluteMinMax = 200;

    /// <summary>Passt die Inhaltshöhe in das Cap ein (aufrunden, mindestens 1).</summary>
    public static int Fit(double contentHeight, double maxHeight) =>
        Math.Max(1, (int)Math.Ceiling(Math.Min(Math.Max(contentHeight, 0), maxHeight)));

    /// <summary>Cap = min(860, Arbeitshöhe − 32), mindestens 200.</summary>
    public static double MaxHeight(double workAreaHeight, double preferredMax = PreferredMaxHeight) =>
        Math.Max(AbsoluteMinMax, Math.Min(preferredMax, workAreaHeight - EdgeMargin));

    /// <summary>Mindest-Fensterhöhe, solange ein ContentDialog offen ist (F008-Fix):
    /// Das atmende Popup schrumpft ohne Konten auf ~200px — darin ist der Dialog
    /// „Konto hinzufügen" gequetscht/nicht bedienbar. Vor ShowAsync temporär auf
    /// diese Höhe wachsen (gecappt), danach via Fit() wieder schrumpfen.</summary>
    public const double DialogMinHeight = 600;

    /// <summary>Fensterhöhe für modale Dialoge: mindestens DialogMin (bzw. Wunsch),
    /// nie über Cap, nie unter aktuelle Höhe (nur wachsen, nie schrumpfen).</summary>
    public static int DialogHeight(int currentHeight, double workAreaHeight, double desiredHeight = DialogMinHeight) =>
        Math.Max(currentHeight, Fit(desiredHeight, MaxHeight(workAreaHeight)));
}
