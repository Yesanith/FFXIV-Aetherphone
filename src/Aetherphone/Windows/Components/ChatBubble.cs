using System.Numerics;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Messaging;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Windows.Components;

internal static class ChatBubble
{
    public static void Draw(ChatLine line, PhoneTheme theme, float entrance = 1f)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var available = ImGui.GetContentRegionAvail().X;
        var padding = 10f * scale;
        var wrap = available * 0.80f - padding * 2f;

        var textSize = ImGui.CalcTextSize(line.Text, false, wrap);
        var bubbleWidth = textSize.X + padding * 2f;
        var bubbleHeight = textSize.Y + padding * 2f;
        var outgoing = line.Direction == MessageDirection.Outgoing;

        var start = ImGui.GetCursorPos();
        var offsetX = outgoing ? available - bubbleWidth : 0f;
        var fillColor = outgoing ? theme.Accent : theme.GroupedCard;
        var textColor = outgoing ? new Vector4(1f, 1f, 1f, 1f) : theme.TextStrong;

        if (entrance < 1f)
        {
            DrawEntering(line.Text, scale, start, offsetX, bubbleWidth, bubbleHeight, padding, wrap, outgoing, fillColor, textColor, entrance);
        }
        else
        {
            ImGui.SetCursorPosX(start.X + offsetX);
            var bubbleScreen = ImGui.GetCursorScreenPos();
            ImGui.GetWindowDrawList().AddRectFilled(bubbleScreen, bubbleScreen + new Vector2(bubbleWidth, bubbleHeight), ImGui.GetColorU32(fillColor), 13f * scale);

            ImGui.SetCursorPos(new Vector2(start.X + offsetX + padding, start.Y + padding));
            ImGui.PushTextWrapPos(start.X + offsetX + padding + wrap);
            using (ImRaii.PushColor(ImGuiCol.Text, textColor))
            {
                ImGui.TextUnformatted(line.Text);
            }
            ImGui.PopTextWrapPos();
        }

        ImGui.SetCursorPos(new Vector2(start.X, start.Y + bubbleHeight + 6f * scale));
    }

    private static void DrawEntering(string text, float scale, Vector2 start, float offsetX, float bubbleWidth, float bubbleHeight, float padding, float wrap, bool outgoing, Vector4 fillColor, Vector4 textColor, float entrance)
    {
        var pop = 0.78f + 0.22f * Easing.EaseOutBack(entrance);
        var alpha = MathF.Min(entrance * 1.8f, 1f);
        var rise = new Vector2(0f, (1f - Easing.EaseOutCubic(entrance)) * 10f * scale);

        var screenStart = ImGui.GetCursorScreenPos();
        var fillMin = screenStart + new Vector2(offsetX, 0f);
        var fillMax = fillMin + new Vector2(bubbleWidth, bubbleHeight);
        var anchor = new Vector2(outgoing ? fillMax.X : fillMin.X, fillMax.Y);
        var scaledMin = anchor + (fillMin - anchor) * pop + rise;
        var scaledMax = anchor + (fillMax - anchor) * pop + rise;
        ImGui.GetWindowDrawList().AddRectFilled(scaledMin, scaledMax, ImGui.GetColorU32(Palette.WithAlpha(fillColor, fillColor.W * alpha)), 13f * scale * pop);

        var textLocal = new Vector2(start.X + offsetX + padding, start.Y + padding);
        var anchorLocal = new Vector2(outgoing ? start.X + offsetX + bubbleWidth : start.X + offsetX, start.Y + bubbleHeight);
        var scaledTextLocal = anchorLocal + (textLocal - anchorLocal) * pop + rise;

        ImGui.SetWindowFontScale(pop);
        ImGui.SetCursorPos(scaledTextLocal);
        ImGui.PushTextWrapPos(scaledTextLocal.X + wrap * pop);
        using (ImRaii.PushColor(ImGuiCol.Text, Palette.WithAlpha(textColor, textColor.W * alpha)))
        {
            ImGui.TextUnformatted(text);
        }
        ImGui.PopTextWrapPos();
        ImGui.SetWindowFontScale(1f);
    }
}
