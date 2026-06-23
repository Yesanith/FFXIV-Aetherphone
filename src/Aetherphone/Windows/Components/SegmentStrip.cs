using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Windows.Components;

internal static class SegmentStrip
{
    public static int Draw(Rect row, IReadOnlyList<string> options, int selected, PhoneTheme theme)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var height = 30f * scale;
        var trackMin = new Vector2(row.Min.X, row.Center.Y - height * 0.5f);
        var trackMax = new Vector2(row.Max.X, row.Center.Y + height * 0.5f);
        var radius = height * 0.5f;

        var dl = ImGui.GetWindowDrawList();
        dl.AddRectFilled(trackMin, trackMax, ImGui.GetColorU32(theme.ToggleOff), radius);

        var segmentWidth = (trackMax.X - trackMin.X) / options.Count;
        var result = selected;
        for (var index = 0; index < options.Count; index++)
        {
            var segmentMin = new Vector2(trackMin.X + index * segmentWidth, trackMin.Y);
            var segmentMax = new Vector2(trackMin.X + (index + 1) * segmentWidth, trackMax.Y);

            if (index == selected)
            {
                var inset = 2f * scale;
                dl.AddRectFilled(segmentMin + new Vector2(inset, inset), segmentMax - new Vector2(inset, inset), ImGui.GetColorU32(theme.Accent), radius - inset);
            }

            var center = new Vector2((segmentMin.X + segmentMax.X) * 0.5f, row.Center.Y);
            Typography.DrawCentered(center, options[index], index == selected ? theme.TextStrong : theme.TextMuted, 0.82f);

            if (!ImGui.IsMouseHoveringRect(segmentMin, segmentMax))
            {
                continue;
            }

            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            {
                result = index;
            }
        }

        return result;
    }
}
