using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Windows.Components;

namespace Aetherphone.Apps.Settings.Pages;

internal sealed class NotificationsPage : ISettingsPage
{
    public string Title => "Notifications";

    public string Summary => configuration.DoNotDisturb ? "Do Not Disturb" : string.Empty;

    public string Glyph => "N";

    public Vector4 Tint => new(0.98f, 0.27f, 0.25f, 1f);

    private readonly Configuration configuration;

    public NotificationsPage(Configuration configuration)
    {
        this.configuration = configuration;
    }

    public void Draw(in PhoneContext context, Rect body)
    {
        var theme = context.Theme;
        using (AppSurface.Begin(body))
        {
            SettingsSection.Header("Alerts", theme);
            var card = GroupCard.Begin(theme, 1);
            var doNotDisturb = SettingsRow.Bool(card.NextRow(), "Do Not Disturb", configuration.DoNotDisturb, theme);
            card.End();

            if (doNotDisturb != configuration.DoNotDisturb)
            {
                configuration.DoNotDisturb = doNotDisturb;
                configuration.Save();
            }
        }
    }
}
