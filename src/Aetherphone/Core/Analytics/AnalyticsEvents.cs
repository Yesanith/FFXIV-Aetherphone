namespace Aetherphone.Core.Analytics;

internal static class AnalyticsEvents
{
    public static AnalyticsEvent SessionStart()
    {
        return new AnalyticsEvent(AnalyticsEventType.SessionStart);
    }

    public static AnalyticsEvent AppOpen(string appId)
    {
        return new AnalyticsEvent(AnalyticsEventType.AppOpen, appId);
    }
}
