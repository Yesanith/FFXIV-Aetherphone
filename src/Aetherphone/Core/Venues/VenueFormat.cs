using System.Globalization;

namespace Aetherphone.Core.Venues;

internal static class VenueFormat
{
    public static string Range(VenueEvent venue)
    {
        var start = venue.StartUtc.ToLocalTime();
        var startText = start.ToString("HH:mm", CultureInfo.InvariantCulture);
        if (!IsToday(start))
        {
            startText = start.ToString("dd/MM HH:mm", CultureInfo.InvariantCulture);
        }

        if (venue.EndUtc is not { } endUtc)
        {
            return startText;
        }

        var end = endUtc.ToLocalTime();
        return $"{startText} – {end:HH:mm}";
    }

    public static string EndsAt(VenueEvent venue)
    {
        if (venue.EndUtc is not { } endUtc)
        {
            return string.Empty;
        }

        return endUtc.ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture);
    }

    public static string Starts(VenueEvent venue, DateTime nowUtc)
    {
        var delta = venue.StartUtc - nowUtc;
        if (delta <= TimeSpan.Zero)
        {
            return Range(venue);
        }

        if (delta < TimeSpan.FromHours(1))
        {
            return $"in {Math.Max(1, (int)delta.TotalMinutes)}m";
        }

        if (delta < TimeSpan.FromDays(1))
        {
            return $"in {(int)delta.TotalHours}h";
        }

        return $"in {(int)delta.TotalDays}d";
    }

    private static bool IsToday(DateTime local) => local.Date == DateTime.Now.Date;
}
