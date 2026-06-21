using System.Numerics;
using Aetherphone.Core.Character;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Utility;
using Dalamud.Plugin.Services;

namespace Aetherphone.Windows.Components;

internal static class GearGrid
{
    private const int Columns = 6;

    public static void Draw(IReadOnlyList<EquippedItem> gear, ITextureProvider textures, PhoneTheme theme)
    {
        if (gear.Count == 0)
        {
            return;
        }

        var scale = ImGuiHelpers.GlobalScale;
        var width = ImGui.GetContentRegionAvail().X;
        var gap = 8f * scale;
        var tile = (width - gap * (Columns - 1)) / Columns;
        var rounding = 8f * scale;
        var origin = ImGui.GetCursorScreenPos();
        var dl = ImGui.GetWindowDrawList();

        for (var index = 0; index < gear.Count; index++)
        {
            var column = index % Columns;
            var rowIndex = index / Columns;
            var min = new Vector2(origin.X + column * (tile + gap), origin.Y + rowIndex * (tile + gap));
            var max = min + new Vector2(tile, tile);

            dl.AddRectFilled(min, max, ImGui.GetColorU32(theme.SurfaceMuted), rounding);

            var texture = textures.GetFromGameIcon(new GameIconLookup(gear[index].IconId)).GetWrapOrEmpty();
            dl.AddImageRounded(texture.Handle, min, max, Vector2.Zero, Vector2.One, 0xFFFFFFFFu, rounding);

            if (ImGui.IsMouseHoveringRect(min, max))
            {
                ImGui.SetTooltip(gear[index].Name);
            }
        }

        var rows = (gear.Count + Columns - 1) / Columns;
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, rows * tile + (rows - 1) * gap));
    }
}
