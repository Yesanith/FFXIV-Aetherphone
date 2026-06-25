using System.Numerics;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Interface.Utility;

namespace Aetherphone.Core.Apps;

internal sealed class PlaceholderApp : IPhoneApp
{
    public string Id { get; }

    public string DisplayName { get; }

    public string Glyph { get; }

    public Vector4 Accent { get; }

    public int BadgeCount => 0;

    public PlaceholderApp(string id, string displayName, string glyph, Vector4 accent)
    {
        Id = id;
        DisplayName = displayName;
        Glyph = glyph;
        Accent = accent;
    }

    public void OnOpened()
    {
    }

    public void OnClosed()
    {
    }

    public void Draw(in PhoneContext context)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var center = context.Content.Center;
        Typography.DrawCentered(center - new Vector2(0f, 14f * scale), DisplayName, context.Theme.TextStrong, 1.4f);
        Typography.DrawCentered(center + new Vector2(0f, 14f * scale), Loc.T(L.Common.ComingSoon), context.Theme.TextMuted);
    }

    public void Dispose()
    {
    }
}
