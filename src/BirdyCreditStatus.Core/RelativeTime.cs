namespace BirdyCreditStatus.Core;

/// <summary>Relativer Reset-Zeitpunkt für die Karten („in …"). Unbekannt (null) → keine Anzeige,
/// vergangen → „gleich". Reine Funktion, ausgelegt für beide Anbieter (F002-T3).</summary>
public static class RelativeTime
{
    public static string Format(DateTimeOffset? resetsAt, DateTimeOffset now)
    {
        if (resetsAt is null)
        {
            return "";
        }

        var diff = resetsAt.Value - now;
        if (diff < TimeSpan.Zero)
        {
            return "gleich";
        }

        if (diff < TimeSpan.FromHours(1))
        {
            var minutes = Math.Max(1, (int)diff.TotalMinutes);
            return minutes == 1 ? "in 1 Minute" : $"in {minutes} Minuten";
        }

        if (diff < TimeSpan.FromHours(24))
        {
            var hours = (int)diff.TotalHours;
            return hours == 1 ? "in 1 Stunde" : $"in {hours} Stunden";
        }

        var days = (int)diff.TotalDays;
        return days == 1 ? "in 1 Tag" : $"in {days} Tagen";
    }
}
