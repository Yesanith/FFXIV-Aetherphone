using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Windows.Components;

internal static class NotificationCard
{
    private const float Height = 66f;
    private const float Gap = 9f;

    public static void Draw(PhoneNotification notification, PhoneTheme theme)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var min = origin;
        var max = new Vector2(origin.X + width, origin.Y + Height * scale);
        var rounding = 16f * scale;

        var dl = ImGui.GetWindowDrawList();
        Elevation.Card(dl, min, max, rounding, scale, 0.7f);
        Squircle.Fill(dl, min, max, rounding, ImGui.GetColorU32(theme.GroupedCard));
        Material.EdgeSquircle(dl, min, max, rounding, scale);

        var tileSize = 38f * scale;
        var tileMin = new Vector2(min.X + 13f * scale, min.Y + (Height * scale - tileSize) * 0.5f);
        var tileMax = tileMin + new Vector2(tileSize, tileSize);
        var tileRounding = tileSize * 0.28f;
        var tint = notification.Accent;
        Squircle.Fill(dl, tileMin, tileMax, tileRounding, ImGui.GetColorU32(tint));

        var gloss = ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.18f));
        dl.AddLine(new Vector2(tileMin.X + tileRounding, tileMin.Y + 1f * scale), new Vector2(tileMax.X - tileRounding, tileMin.Y + 1f * scale), gloss, 1f * scale);

        var iconCenter = (tileMin + tileMax) * 0.5f;
        var ink = new Vector4(0.99f, 0.99f, 1f, 1f);
        var hole = Palette.Mix(tint, new Vector4(0f, 0f, 0f, 1f), 0.25f);
        if (!AppIconArt.TryDraw(notification.AppId, iconCenter, tileSize * 0.5f, ink, hole))
        {
            dl.AddCircleFilled(iconCenter, 4f * scale, ImGui.GetColorU32(ink), 16);
        }

        var textLeft = tileMax.X + 12f * scale;
        var textRight = max.X - 14f * scale;

        var time = RelativeTime(notification.ReceivedAt);
        var timeSize = Typography.Measure(time, TextStyles.Caption1);
        Typography.Draw(new Vector2(textRight - timeSize.X, min.Y + 13f * scale), time, theme.TextMuted, TextStyles.Caption1);

        dl.PushClipRect(new Vector2(textLeft, min.Y), new Vector2(textRight - timeSize.X - 8f * scale, max.Y), true);
        Typography.Draw(new Vector2(textLeft, min.Y + 12f * scale), notification.Title, theme.TextStrong, TextStyles.Headline);
        dl.PopClipRect();

        dl.PushClipRect(new Vector2(textLeft, min.Y), new Vector2(textRight, max.Y), true);
        Typography.Draw(new Vector2(textLeft, min.Y + 35f * scale), notification.Body, theme.TextMuted, TextStyles.Subheadline);
        dl.PopClipRect();

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, (Height + Gap) * scale));
    }

    internal static string RelativeTime(DateTime time)
    {
        var delta = DateTime.Now - time;
        if (delta.TotalMinutes < 1)
        {
            return Loc.T(L.Time.Now);
        }

        if (delta.TotalHours < 1)
        {
            return Loc.T(L.Time.MinutesShort, (int)delta.TotalMinutes);
        }

        if (delta.TotalDays < 1)
        {
            return Loc.T(L.Time.HoursShort, (int)delta.TotalHours);
        }

        return Loc.T(L.Time.DaysShort, (int)delta.TotalDays);
    }
}
