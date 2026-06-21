namespace Aetherphone.Core.Theme;

internal enum WallpaperStyle
{
    Dusk,
    Aurora,
    Ocean,
    Ember,
    Mono,
}

internal static class WallpaperCatalog
{
    public static readonly IReadOnlyList<WallpaperStyle> All = new[]
    {
        WallpaperStyle.Dusk,
        WallpaperStyle.Aurora,
        WallpaperStyle.Ocean,
        WallpaperStyle.Ember,
        WallpaperStyle.Mono,
    };

    public static WallpaperStyle Resolve(string name) => Enum.TryParse(name, out WallpaperStyle style) ? style : WallpaperStyle.Dusk;

    public static int IndexOf(WallpaperStyle style)
    {
        for (var index = 0; index < All.Count; index++)
        {
            if (All[index] == style)
            {
                return index;
            }
        }

        return 0;
    }
}
