using System.Numerics;
using Aetherphone.Core.Messaging;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Windows.Components;

internal static class ConversationRow
{
    private const float Height = 60f;

    public static bool Draw(Conversation conversation, PhoneTheme theme)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var min = origin;
        var max = new Vector2(origin.X + width, origin.Y + Height * scale);
        var hovered = ImGui.IsMouseHoveringRect(min, max);

        var dl = ImGui.GetWindowDrawList();
        if (hovered)
        {
            dl.AddRectFilled(min, max, ImGui.GetColorU32(theme.GroupedCard), 12f * scale);
        }

        var avatarRadius = 20f * scale;
        var avatarCenter = new Vector2(min.X + 14f * scale + avatarRadius, min.Y + Height * scale * 0.5f);
        dl.AddCircleFilled(avatarCenter, avatarRadius, ImGui.GetColorU32(theme.Accent), 32);
        Typography.DrawCentered(avatarCenter, Initial(conversation.Contact), new Vector4(1f, 1f, 1f, 1f), 1.1f);

        var textLeft = avatarCenter.X + avatarRadius + 12f * scale;
        var textRight = max.X - 14f * scale;

        Typography.Draw(new Vector2(textLeft, min.Y + 12f * scale), conversation.Contact, theme.TextStrong);

        var preview = conversation.Last?.Text ?? string.Empty;
        dl.PushClipRect(new Vector2(textLeft, min.Y), new Vector2(textRight, max.Y), true);
        Typography.Draw(new Vector2(textLeft, min.Y + 33f * scale), preview, theme.TextMuted, 0.9f);
        dl.PopClipRect();

        if (conversation.Unread > 0)
        {
            dl.AddCircleFilled(new Vector2(textRight - 5f * scale, min.Y + 18f * scale), 5f * scale, ImGui.GetColorU32(theme.Accent), 16);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, Height * scale));

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }

    private static string Initial(string contact) => contact.Length > 0 ? contact.Substring(0, 1).ToUpperInvariant() : "?";
}
