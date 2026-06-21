using System.Numerics;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Windows.Components;

internal static class AppIcon
{
    public static bool Draw(Vector2 center, float size, IPhoneApp app, PhoneTheme theme)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var half = size * 0.5f;
        var min = center - new Vector2(half, half);
        var max = center + new Vector2(half, half);
        var hovered = ImGui.IsMouseHoveringRect(min, max);

        var dl = ImGui.GetWindowDrawList();
        var fill = hovered ? Palette.Mix(app.Accent, theme.TextStrong, 0.14f) : app.Accent;
        dl.AddRectFilled(min, max, ImGui.GetColorU32(fill), size * 0.26f);

        if (!AppIconArt.TryDraw(app.Id, center, size, theme.TextStrong, fill))
        {
            var glyphHeight = Typography.Measure(app.Glyph).Y;
            var glyphScale = glyphHeight > 0f ? size * 0.5f / glyphHeight : 1f;
            Typography.DrawCentered(center, app.Glyph, theme.TextStrong, glyphScale);
        }

        Typography.DrawCentered(new Vector2(center.X, max.Y + 11f * scale), app.DisplayName, Palette.WithAlpha(theme.TextStrong, 0.95f), 0.85f);

        if (app.BadgeCount > 0)
        {
            DrawBadge(new Vector2(max.X - 5f * scale, min.Y + 5f * scale), app.BadgeCount, theme, scale);
        }

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }

    private static void DrawBadge(Vector2 center, int count, PhoneTheme theme, float scale)
    {
        var dl = ImGui.GetWindowDrawList();
        dl.AddCircleFilled(center, 9f * scale, ImGui.GetColorU32(theme.Danger), 24);
        var label = count > 99 ? "99+" : count.ToString();
        Typography.DrawCentered(center, label, theme.TextStrong, 0.7f);
    }
}
