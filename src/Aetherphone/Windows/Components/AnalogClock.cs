using System.Numerics;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Windows.Components;

internal static class AnalogClock
{
    public static void Draw(Vector2 center, float radius, float hours, float minutes, float seconds, PhoneTheme theme)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var dl = ImGui.GetWindowDrawList();

        dl.AddCircleFilled(center, radius, ImGui.GetColorU32(theme.ScreenBase), 48);
        dl.AddCircle(center, radius, ImGui.GetColorU32(theme.Separator), 48, 1.4f * scale);

        for (var tick = 0; tick < 12; tick++)
        {
            var angle = tick * (MathF.PI / 6f) - MathF.PI / 2f;
            var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            var major = tick % 3 == 0;
            var inner = center + direction * radius * (major ? 0.72f : 0.80f);
            var outer = center + direction * radius * 0.90f;
            dl.AddLine(inner, outer, ImGui.GetColorU32(theme.TextMuted), (major ? 1.8f : 1f) * scale);
        }

        var hourAngle = (hours % 12f + minutes / 60f) * (MathF.PI / 6f) - MathF.PI / 2f;
        var minuteAngle = (minutes + seconds / 60f) * (MathF.PI / 30f) - MathF.PI / 2f;
        var secondAngle = seconds * (MathF.PI / 30f) - MathF.PI / 2f;

        Hand(center, hourAngle, radius * 0.50f, 3.2f * scale, theme.TextStrong);
        Hand(center, minuteAngle, radius * 0.74f, 2.4f * scale, theme.TextStrong);
        Hand(center, secondAngle, radius * 0.82f, 1.2f * scale, theme.Accent);

        dl.AddCircleFilled(center, 2.6f * scale, ImGui.GetColorU32(theme.Accent), 12);
    }

    private static void Hand(Vector2 center, float angle, float length, float thickness, Vector4 color)
    {
        var tip = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * length;
        ImGui.GetWindowDrawList().AddLine(center, tip, ImGui.GetColorU32(color), thickness);
    }
}
