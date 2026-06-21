using System.Numerics;
using Aetherphone.Core.Character;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Windows.Components;

internal static class CharacterHeader
{
    public static void Draw(LocalCharacter character, PhoneTheme theme)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var width = ImGui.GetContentRegionAvail().X;
        var origin = ImGui.GetCursorScreenPos();
        var centerX = origin.X + width * 0.5f;
        var dl = ImGui.GetWindowDrawList();

        var radius = 34f * scale;
        var avatarCenter = new Vector2(centerX, origin.Y + radius + 6f * scale);
        dl.AddCircleFilled(avatarCenter, radius, ImGui.GetColorU32(theme.Accent), 48);
        Typography.DrawCentered(avatarCenter, Initial(character.Name), new Vector4(1f, 1f, 1f, 1f), 2.0f);

        var nameY = avatarCenter.Y + radius + 18f * scale;
        Typography.DrawCentered(new Vector2(centerX, nameY), character.Name, theme.TextStrong, 1.5f);

        var worldLine = character.DataCenter.Length > 0 ? $"{character.WorldName} [{character.DataCenter}]" : character.WorldName;
        Typography.DrawCentered(new Vector2(centerX, nameY + 26f * scale), worldLine, theme.TextMuted, 0.95f);

        Typography.DrawCentered(new Vector2(centerX, nameY + 48f * scale), JobLine(character), theme.TextMuted, 0.9f);

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, radius * 2f + 86f * scale));
    }

    private static string JobLine(LocalCharacter character)
    {
        var job = character.Job.Length > 0 ? $"Lv {character.Level} · {character.Job}" : $"Lv {character.Level}";
        return character.AverageItemLevel > 0 ? $"{job} · i{character.AverageItemLevel}" : job;
    }

    private static string Initial(string name) => name.Length > 0 ? name.Substring(0, 1).ToUpperInvariant() : "?";
}
