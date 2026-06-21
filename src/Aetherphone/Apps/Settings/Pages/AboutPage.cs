using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Windows.Components;

namespace Aetherphone.Apps.Settings.Pages;

internal sealed class AboutPage : ISettingsPage
{
    public string Title => "About";

    public string Summary => AepConstants.Version;

    public string Glyph => "i";

    public Vector4 Tint => new(0.40f, 0.62f, 0.92f, 1f);

    private readonly Action showAbout;

    public AboutPage(Action showAbout)
    {
        this.showAbout = showAbout;
    }

    public void Draw(in PhoneContext context, Rect body)
    {
        var theme = context.Theme;
        using (AppSurface.Begin(body))
        {
            SettingsSection.Header("Information", theme);
            var card = GroupCard.Begin(theme, 3);
            SettingsRow.Info(card.NextRow(), "Plugin", AepConstants.Name, theme);
            SettingsRow.Info(card.NextRow(), "Version", AepConstants.Version, theme);
            SettingsRow.Info(card.NextRow(), "Command", AepConstants.PrimaryCommand, theme);
            card.End();

            SettingsSection.Header("Credits & links", theme);
            var links = GroupCard.Begin(theme, 1);
            if (SettingsRow.Link(links.NextRow(), Glyph, Tint, "About Aetherphone", string.Empty, theme))
            {
                showAbout();
            }
            links.End();
        }
    }
}
