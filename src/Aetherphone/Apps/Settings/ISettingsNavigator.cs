namespace Aetherphone.Apps.Settings;

// The in-app navigation surface a page uses to drill into a child page. Implemented by
// SettingsApp, which keeps the page stack so the device header's back chevron pops it.
internal interface ISettingsNavigator
{
    void Open(ISettingsPage page);
}
