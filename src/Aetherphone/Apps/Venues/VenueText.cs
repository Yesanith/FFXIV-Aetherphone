using Aetherphone.Core;
using Aetherphone.Windows.Components;

namespace Aetherphone.Apps.Venues;

internal static class VenueText
{
    private const string Ellipsis = "…";

    public static string Fit(string text, float maxWidth, float scale, FontWeight weight)
    {
        if (text.Length == 0 || maxWidth <= 0f)
        {
            return text;
        }

        if (Typography.Measure(text, scale, weight).X <= maxWidth)
        {
            return text;
        }

        for (var length = text.Length - 1; length > 0; length--)
        {
            var candidate = text.Substring(0, length).TrimEnd() + Ellipsis;
            if (Typography.Measure(candidate, scale, weight).X <= maxWidth)
            {
                return candidate;
            }
        }

        return Ellipsis;
    }
}
