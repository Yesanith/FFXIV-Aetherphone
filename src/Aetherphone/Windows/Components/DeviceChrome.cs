using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Windows.Components;

internal static class DeviceChrome
{
    public static Rect DrawBody(Rect device, PhoneTheme theme)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var dl = ImGui.GetWindowDrawList();

        var deviceRounding = theme.DeviceRounding * scale;
        dl.AddRectFilled(device.Min, device.Max, ImGui.GetColorU32(theme.BezelOuter), deviceRounding);
        dl.AddRect(device.Min, device.Max, ImGui.GetColorU32(theme.BezelRim), deviceRounding);

        var screen = device.Inset(theme.BezelThickness * scale);
        dl.AddRectFilled(screen.Min, screen.Max, ImGui.GetColorU32(theme.ScreenBase), theme.ScreenRounding * scale);
        return screen;
    }

    public static void FillScreen(Rect screen, PhoneTheme theme, Vector4 color)
    {
        var scale = ImGuiHelpers.GlobalScale;
        ImGui.GetWindowDrawList().AddRectFilled(screen.Min, screen.Max, ImGui.GetColorU32(color), theme.ScreenRounding * scale);
    }

    public static void DrawWallpaper(Rect screen, PhoneTheme theme)
    {
        var dl = ImGui.GetWindowDrawList();
        dl.PushClipRect(screen.Min, screen.Max, true);
        WallpaperPainter.Paint(theme.Wallpaper, screen);
        dl.PopClipRect();

        RoundCorners(screen, theme);
    }

    private static readonly float[] CornerStartAngles = { MathF.PI, MathF.PI * 1.5f, 0f, MathF.PI * 0.5f };

    private static void RoundCorners(Rect screen, PhoneTheme theme)
    {
        const int cornerSegments = 24;
        const float quarterTurn = MathF.PI * 0.5f;

        var radius = theme.ScreenRounding * ImGuiHelpers.GlobalScale;
        var dl = ImGui.GetWindowDrawList();
        var bezel = ImGui.GetColorU32(theme.BezelOuter);

        for (var index = 0; index < CornerStartAngles.Length; index++)
        {
            var (corner, center) = CornerGeometry(screen, radius, index);
            var startAngle = CornerStartAngles[index];

            dl.PathClear();
            dl.PathLineTo(corner);
            dl.PathArcTo(center, radius, startAngle, startAngle + quarterTurn, cornerSegments);
            dl.PathFillConvex(bezel);
        }
    }

    private static (Vector2 Corner, Vector2 Center) CornerGeometry(Rect screen, float radius, int index)
    {
        return index switch
        {
            0 => (new Vector2(screen.Min.X, screen.Min.Y), new Vector2(screen.Min.X + radius, screen.Min.Y + radius)),
            1 => (new Vector2(screen.Max.X, screen.Min.Y), new Vector2(screen.Max.X - radius, screen.Min.Y + radius)),
            2 => (new Vector2(screen.Max.X, screen.Max.Y), new Vector2(screen.Max.X - radius, screen.Max.Y - radius)),
            _ => (new Vector2(screen.Min.X, screen.Max.Y), new Vector2(screen.Min.X + radius, screen.Max.Y - radius)),
        };
    }

    public static void DrawIsland(Rect screen, PhoneTheme theme)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var width = 98f * scale;
        var height = 26f * scale;
        var top = screen.Min.Y + 9f * scale;
        var min = new Vector2(screen.Center.X - width * 0.5f, top);
        var max = new Vector2(screen.Center.X + width * 0.5f, top + height);
        ImGui.GetWindowDrawList().AddRectFilled(min, max, ImGui.GetColorU32(theme.BezelOuter), height * 0.5f);
    }
}
