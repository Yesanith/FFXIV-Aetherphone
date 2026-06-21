using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Dalamud.Interface.Utility;

namespace Aetherphone.Windows.Components;

internal static class ProfileRow
{
    public const float RowHeight = 54f;

    public static void Stacked(Rect row, string label, string value, PhoneTheme theme)
    {
        var scale = ImGuiHelpers.GlobalScale;
        Typography.Draw(new Vector2(row.Min.X, row.Min.Y + 9f * scale), label, theme.TextMuted, 0.78f);
        Typography.Draw(new Vector2(row.Min.X, row.Min.Y + 27f * scale), value, theme.TextStrong);
    }
}
