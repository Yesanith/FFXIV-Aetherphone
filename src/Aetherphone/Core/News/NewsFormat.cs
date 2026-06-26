using Aetherphone.Core.Localization;

namespace Aetherphone.Core.News;

internal enum MaintenanceStatus : byte
{
    None,
    Upcoming,
    Active,
    Done,
}

internal static class NewsFormat
{
    public static string Ago(DateTimeOffset time)
    {
        var delta = DateTimeOffset.UtcNow - time.ToUniversalTime();
        if (delta < TimeSpan.Zero)
        {
            delta = TimeSpan.Zero;
        }

        if (delta.TotalSeconds < 60)
        {
            return Loc.T(L.Time.JustNow);
        }

        if (delta.TotalMinutes < 60)
        {
            return Loc.T(L.Time.MinutesAgo, (int)delta.TotalMinutes);
        }

        if (delta.TotalHours < 24)
        {
            return Loc.T(L.Time.HoursAgo, (int)delta.TotalHours);
        }

        return Loc.T(L.Time.DaysAgo, (int)delta.TotalDays);
    }

    public static string Window(DateTimeOffset start, DateTimeOffset end)
    {
        var localStart = start.ToLocalTime();
        var localEnd = end.ToLocalTime();
        var startText = localStart.ToString("MMM d, HH:mm", Loc.Culture);
        var endText = localStart.Date == localEnd.Date
            ? localEnd.ToString("HH:mm", Loc.Culture)
            : localEnd.ToString("MMM d, HH:mm", Loc.Culture);
        return string.Concat(startText, " – ", endText);
    }

    public static MaintenanceStatus Status(DateTimeOffset? start, DateTimeOffset? end)
    {
        if (start is not { } startTime || end is not { } endTime)
        {
            return MaintenanceStatus.None;
        }

        var now = DateTimeOffset.UtcNow;
        if (now < startTime.ToUniversalTime())
        {
            return MaintenanceStatus.Upcoming;
        }

        return now <= endTime.ToUniversalTime() ? MaintenanceStatus.Active : MaintenanceStatus.Done;
    }

    public static string Clip(string value, int maxLength)
    {
        if (value.Length <= maxLength)
        {
            return value;
        }

        return string.Concat(value.AsSpan(0, maxLength - 1), "…");
    }
}
