namespace Aetherphone.Core.Analytics;

internal interface IAnalyticsService : IDisposable
{
    void Track(AnalyticsEvent analyticsEvent);
}
