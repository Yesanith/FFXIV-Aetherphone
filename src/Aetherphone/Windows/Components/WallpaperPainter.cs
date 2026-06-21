using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class WallpaperPainter
{
    public static void Paint(WallpaperStyle style, Rect area)
    {
        switch (style)
        {
            case WallpaperStyle.Aurora:
                VerticalGradient(area, new Vector4(0.18f, 0.13f, 0.32f, 1f), new Vector4(0.05f, 0.09f, 0.15f, 1f));
                Blob(new Vector2(area.Min.X + area.Width * 0.22f, area.Min.Y + area.Height * 0.18f), area.Width * 0.62f, new Vector4(0.50f, 0.32f, 0.88f, 1f));
                Blob(new Vector2(area.Max.X - area.Width * 0.18f, area.Min.Y + area.Height * 0.52f), area.Width * 0.58f, new Vector4(0.20f, 0.62f, 0.60f, 1f));
                break;
            case WallpaperStyle.Ocean:
                VerticalGradient(area, new Vector4(0.10f, 0.21f, 0.42f, 1f), new Vector4(0.03f, 0.06f, 0.14f, 1f));
                Blob(new Vector2(area.Center.X, area.Max.Y - area.Height * 0.12f), area.Width * 0.80f, new Vector4(0.22f, 0.50f, 0.85f, 1f));
                break;
            case WallpaperStyle.Ember:
                VerticalGradient(area, new Vector4(0.16f, 0.08f, 0.16f, 1f), new Vector4(0.30f, 0.11f, 0.07f, 1f));
                Blob(new Vector2(area.Center.X, area.Max.Y - area.Height * 0.08f), area.Width * 0.85f, new Vector4(0.92f, 0.45f, 0.20f, 1f));
                break;
            case WallpaperStyle.Mono:
                VerticalGradient(area, new Vector4(0.13f, 0.13f, 0.15f, 1f), new Vector4(0.05f, 0.05f, 0.06f, 1f));
                Blob(new Vector2(area.Center.X, area.Min.Y + area.Height * 0.30f), area.Width * 0.70f, new Vector4(0.32f, 0.32f, 0.38f, 1f));
                break;
            default:
                VerticalGradient(area, new Vector4(0.15f, 0.13f, 0.27f, 1f), new Vector4(0.04f, 0.04f, 0.08f, 1f));
                Blob(new Vector2(area.Center.X, area.Min.Y + area.Height * 0.12f), area.Width * 0.70f, new Vector4(0.45f, 0.38f, 0.90f, 1f));
                break;
        }
    }

    private static void VerticalGradient(Rect area, Vector4 top, Vector4 bottom)
    {
        const int bands = 48;
        var dl = ImGui.GetWindowDrawList();
        var bandHeight = area.Height / bands;
        for (var index = 0; index < bands; index++)
        {
            var color = Vector4.Lerp(top, bottom, index / (float)bands);
            var y = area.Min.Y + index * bandHeight;
            dl.AddRectFilled(new Vector2(area.Min.X, y), new Vector2(area.Max.X, y + bandHeight + 1f), ImGui.GetColorU32(color));
        }
    }

    private static void Blob(Vector2 center, float radius, Vector4 color)
    {
        var dl = ImGui.GetWindowDrawList();
        for (var ring = 3; ring >= 1; ring--)
        {
            dl.AddCircleFilled(center, radius * (0.45f + ring * 0.18f), ImGui.GetColorU32(Palette.WithAlpha(color, 0.05f * ring)), 48);
        }
    }
}
