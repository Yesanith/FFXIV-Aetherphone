using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Windows.Components;

internal static class Typography
{
    public static Vector2 Measure(string text, float scale = 1f)
    {
        using (Plugin.Fonts.Push(scale))
        {
            return ImGui.CalcTextSize(text);
        }
    }

    public static void Draw(Vector2 position, string text, Vector4 color, float scale = 1f)
    {
        using (Plugin.Fonts.Push(scale))
        {
            ImGui.SetCursorScreenPos(position);
            using (ImRaii.PushColor(ImGuiCol.Text, color))
            {
                ImGui.TextUnformatted(text);
            }
        }
    }

    public static void DrawCentered(Vector2 center, string text, Vector4 color, float scale = 1f)
    {
        using (Plugin.Fonts.Push(scale))
        {
            var size = ImGui.CalcTextSize(text);
            ImGui.SetCursorScreenPos(center - size * 0.5f);
            using (ImRaii.PushColor(ImGuiCol.Text, color))
            {
                ImGui.TextUnformatted(text);
            }
        }
    }
}
