using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;

namespace Aetherphone.Apps.Settings.Pages;

internal sealed class AppearancePage : ISettingsPage
{
    public string Title => "Appearance";

    public string Summary => configuration.AccentName;

    public string Glyph => "A";

    public Vector4 Tint => new(0.55f, 0.45f, 0.95f, 1f);

    private readonly Configuration configuration;
    private readonly ThemeProvider themes;

    public AppearancePage(Configuration configuration, ThemeProvider themes)
    {
        this.configuration = configuration;
        this.themes = themes;
    }

    public void Draw(in PhoneContext context, Rect body)
    {
        var theme = context.Theme;
        using (AppSurface.Begin(body))
        {
            SettingsSection.Header("Theme", theme);
            var card = GroupCard.Begin(theme, 2);

            var accentIndex = SwatchStrip.Draw(card.NextRow(), "Accent", ThemeCatalog.Accents, ThemeCatalog.IndexOf(ThemeCatalog.Accents, configuration.AccentName), theme);
            var accentName = ThemeCatalog.Accents[accentIndex].Name;
            if (accentName != configuration.AccentName)
            {
                configuration.AccentName = accentName;
                ApplyTheme();
            }

            var wallpaperIndex = WallpaperStrip.Draw(card.NextRow(), "Wallpaper", WallpaperCatalog.All, WallpaperCatalog.IndexOf(WallpaperCatalog.Resolve(configuration.WallpaperName)), theme);
            var wallpaperName = WallpaperCatalog.All[wallpaperIndex].ToString();
            if (wallpaperName != configuration.WallpaperName)
            {
                configuration.WallpaperName = wallpaperName;
                ApplyTheme();
            }

            card.End();

            SettingsSection.Header("Text Size", theme);
            var zoomCard = GroupCard.Begin(theme, 1);
            var zoomIndex = SegmentStrip.Draw(zoomCard.NextRow(), TextZoomCatalog.Labels, TextZoomCatalog.IndexOf(configuration.TextZoom), theme);
            zoomCard.End();

            var zoom = TextZoomCatalog.Scales[zoomIndex];
            if (MathF.Abs(zoom - configuration.TextZoom) > 0.001f)
            {
                configuration.TextZoom = zoom;
                Plugin.Fonts.SetZoom(zoom);
                configuration.Save();
            }
        }
    }

    private void ApplyTheme()
    {
        themes.Apply(configuration);
        configuration.Save();
    }
}
