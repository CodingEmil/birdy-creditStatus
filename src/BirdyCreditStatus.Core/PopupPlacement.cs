namespace BirdyCreditStatus.Core;

/// <summary>Arbeitsbereich eines Monitors (reine Geometrie, UI-unabhängig).</summary>
public sealed record ScreenArea(int X, int Y, int Width, int Height);

/// <summary>Popup-Platzierung (F002-Fix, symmetrisch seit F008): Das Popup öffnet unten
/// rechts mit einheitlichem Abstand (16px) zur rechten und unteren Arbeitsbereichs-
/// kante. Reine Funktionen.</summary>
public static class PopupPlacement
{
    private const int Margin = 16;

    public static ScreenArea PickWorkArea(
        IReadOnlyList<ScreenArea> areas, int cursorX, int cursorY, ScreenArea fallback) =>
        areas.FirstOrDefault(a =>
            cursorX >= a.X && cursorX < a.X + a.Width
            && cursorY >= a.Y && cursorY < a.Y + a.Height)
        ?? fallback;

    public static (int X, int Y) BottomRight(ScreenArea work, int windowWidth, int windowHeight) =>
        (work.X + work.Width - windowWidth - Margin, work.Y + work.Height - windowHeight - Margin);
}
