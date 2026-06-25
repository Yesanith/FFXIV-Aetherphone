using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Character;
using Aetherphone.Core.Game;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Lodestone;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Plugin.Services;

namespace Aetherphone.Apps.MyCharacter;

internal sealed class MyCharacterApp : IPhoneApp
{
    private const float RefreshIntervalSeconds = 3f;

    public string Id => "character";

    public string DisplayName => Loc.T(L.Apps.Character);

    public string Glyph => "Me";

    public Vector4 Accent => new(0.36f, 0.72f, 0.62f, 1f);

    public int BadgeCount => 0;

    private readonly GameData gameData;
    private readonly ITextureProvider textures;
    private readonly LodestoneService lodestone;

    private LocalCharacter? character;
    private float sinceRefresh;

    public MyCharacterApp(GameData gameData, ITextureProvider textures, LodestoneService lodestone)
    {
        this.gameData = gameData;
        this.textures = textures;
        this.lodestone = lodestone;
    }

    public void OnOpened() => Refresh();

    public void OnClosed()
    {
    }

    private void Refresh()
    {
        character = LocalCharacterReader.Read(gameData);
        sinceRefresh = 0f;
    }

    public void Draw(in PhoneContext context)
    {
        sinceRefresh += ImGui.GetIO().DeltaTime;
        if (sinceRefresh >= RefreshIntervalSeconds)
        {
            Refresh();
        }

        AppHeader.Draw(context, DisplayName);

        var scale = ImGuiHelpers.GlobalScale;
        var theme = context.Theme;
        var content = context.Content;
        var body = new Rect(new Vector2(content.Min.X, content.Min.Y + AppHeader.Height * scale), content.Max);

        if (character is not { } snapshot)
        {
            Typography.DrawCentered(body.Center, Loc.T(L.Character.LogInToView), theme.TextMuted);
            return;
        }

        using (AppSurface.Begin(body))
        {
            CharacterHeader.Draw(snapshot, theme, lodestone);

            SettingsSection.Header(Loc.T(L.Character.Profile), theme);
            var card = GroupCard.Begin(theme, 7, ProfileRow.RowHeight);
            ProfileRow.Stacked(card.NextRow(), Loc.T(L.Character.Race), snapshot.Race, theme);
            ProfileRow.Stacked(card.NextRow(), Loc.T(L.Character.Clan), snapshot.Clan, theme);
            ProfileRow.Stacked(card.NextRow(), Loc.T(L.Character.Gender), snapshot.Gender, theme);
            ProfileRow.Stacked(card.NextRow(), Loc.T(L.Character.Nameday), snapshot.Nameday, theme);
            ProfileRow.Stacked(card.NextRow(), Loc.T(L.Character.Guardian), snapshot.Guardian, theme);
            ProfileRow.Stacked(card.NextRow(), Loc.T(L.Character.CityState), snapshot.CityState, theme);
            ProfileRow.Stacked(card.NextRow(), Loc.T(L.Character.GrandCompany), snapshot.GrandCompany, theme);
            card.End();

            if (snapshot.Gear.Count > 0)
            {
                SettingsSection.Header(Loc.T(L.Character.Equipment), theme);
                GearGrid.Draw(snapshot.Gear, textures, theme);
            }
        }
    }

    public void Dispose()
    {
    }
}
