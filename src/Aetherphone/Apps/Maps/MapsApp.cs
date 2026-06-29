using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Maps;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Venues;
using Aetherphone.Windows;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Plugin.Services;

namespace Aetherphone.Apps.Maps;

internal sealed class MapsApp : IPhoneApp
{
    private const float SearchHeight = 46f;
    private const float LocationCardHeight = 64f;
    private const float ZoneRowHeight = 50f;
    private const float AetheryteRowHeight = 56f;
    private const float TravelButtonWidth = 86f;
    private const float MapPreviewMaxHeight = 200f;

    private static readonly Vector4 MapAccent = new(0.20f, 0.62f, 0.86f, 1f);

    public string Id => "maps";

    public string DisplayName => Loc.T(L.Apps.Maps);

    public string Glyph => "Ma";

    public Vector4 Accent => MapAccent;

    public int BadgeCount => 0;

    private readonly MapData maps;
    private readonly MapTextureCache textureCache;

    private readonly ViewRouter<MapZone?> router;
    private readonly RouterDraw<MapZone?> drawView;
    private readonly Action backToList;

    private readonly List<MapZone> searchResults = new();

    private string search = string.Empty;
    private bool lifestreamAvailable;

    private PhoneTheme frameTheme = PhoneTheme.Default;
    private INavigator frameNavigation = null!;

    public MapsApp(MapData maps, ITextureProvider textures)
    {
        this.maps = maps;
        textureCache = new MapTextureCache(textures);

        router = new ViewRouter<MapZone?>(null);
        drawView = DrawView;
        backToList = () => router.Pop();
    }

    public void OnOpened()
    {
        router.Reset();
        search = string.Empty;
        lifestreamAvailable = LifestreamBridge.IsAvailable();
    }

    public void OnClosed()
    {
        router.Reset();
        search = string.Empty;
    }

    public void Draw(in PhoneContext context)
    {
        frameTheme = context.Theme;
        frameNavigation = context.Navigation;
        router.Draw(context.Content, context.Theme.AppBackground, ImGui.GetIO().DeltaTime, drawView);
    }

    private void DrawView(MapZone? view, Rect area, int depth)
    {
        if (view is { } zone)
        {
            DrawDetail(area, zone);
        }
        else
        {
            DrawRoot(area);
        }
    }

    private void DrawRoot(Rect area)
    {
        var context = new PhoneContext(area, frameTheme, frameNavigation);
        AppHeader.Draw(context, DisplayName);

        var scale = ImGuiHelpers.GlobalScale;
        var pad = 16f * scale;
        var top = area.Min.Y + AppHeader.Height * scale;

        var searchBar = new Rect(new Vector2(area.Min.X + pad, top), new Vector2(area.Max.X - pad, top + SearchHeight * scale));
        SearchField.Draw(searchBar, "##mapsSearch", Loc.T(L.Maps.Search), ref search, frameTheme, 60);

        var body = new Rect(new Vector2(area.Min.X, searchBar.Max.Y), area.Max);
        using (AppSurface.Begin(body))
        {
            DrawLocationCard();

            if (search.Length > 0)
            {
                DrawSearchResults(body);
            }
            else
            {
                DrawRegions();
            }

            ImGui.Dummy(new Vector2(0f, 10f * scale));
        }
    }

    private void DrawLocationCard()
    {
        var scale = ImGuiHelpers.GlobalScale;
        var location = maps.CurrentLocation();
        var zoneName = location.Zone.Length > 0 ? location.Zone : Loc.T(L.Maps.Unknown);
        var regionName = location.Region.Length > 0 ? location.Region : Loc.T(L.Maps.Unknown);

        SettingsSection.Header(Loc.T(L.Maps.CurrentLocation), frameTheme);

        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var cardMin = origin;
        var cardMax = new Vector2(origin.X + width, origin.Y + LocationCardHeight * scale);
        var drawList = ImGui.GetWindowDrawList();
        Squircle.Fill(drawList, cardMin, cardMax, 14f * scale, ImGui.GetColorU32(frameTheme.GroupedCard));
        Material.EdgeSquircle(drawList, cardMin, cardMax, 14f * scale, scale);

        var pinCenter = new Vector2(cardMin.X + 28f * scale, cardMin.Y + LocationCardHeight * scale * 0.5f);
        DrawPin(drawList, pinCenter, 12f * scale, frameTheme.Accent);

        var textLeft = pinCenter.X + 24f * scale;
        Typography.Draw(new Vector2(textLeft, cardMin.Y + 14f * scale), zoneName, frameTheme.TextStrong, TextStyles.Headline);
        Typography.Draw(new Vector2(textLeft, cardMin.Y + 36f * scale), regionName, frameTheme.TextMuted, TextStyles.Footnote);

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, LocationCardHeight * scale + 4f * scale));

        if (maps.TryGetCurrentZone(out var zone) && ImGui.IsMouseHoveringRect(cardMin, cardMax))
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            {
                router.Push(zone);
            }
        }
    }

    private void DrawRegions()
    {
        var regions = maps.Regions;
        if (regions.Count == 0)
        {
            DrawEmptyState(Loc.T(L.Maps.NoZones));
            return;
        }

        for (var regionIndex = 0; regionIndex < regions.Count; regionIndex++)
        {
            var region = regions[regionIndex];
            var zones = region.Zones;
            if (zones.Count == 0)
            {
                continue;
            }

            SettingsSection.Header(region.Name, frameTheme);
            var card = GroupCard.Begin(frameTheme, zones.Count, ZoneRowHeight);
            for (var zoneIndex = 0; zoneIndex < zones.Count; zoneIndex++)
            {
                if (DrawZoneRow(card.NextRow(), zones[zoneIndex]))
                {
                    router.Push(zones[zoneIndex]);
                }
            }

            card.End();
        }
    }

    private void DrawSearchResults(Rect body)
    {
        CollectSearchResults();
        if (searchResults.Count == 0)
        {
            DrawEmptyState(Loc.T(L.Maps.NoZones));
            return;
        }

        SettingsSection.Header(Loc.T(L.Maps.ZonesCount, searchResults.Count), frameTheme);
        var card = GroupCard.Begin(frameTheme, searchResults.Count, ZoneRowHeight);
        for (var index = 0; index < searchResults.Count; index++)
        {
            if (DrawZoneRow(card.NextRow(), searchResults[index]))
            {
                router.Push(searchResults[index]);
            }
        }

        card.End();
    }

    private void CollectSearchResults()
    {
        searchResults.Clear();
        var regions = maps.Regions;
        for (var regionIndex = 0; regionIndex < regions.Count; regionIndex++)
        {
            var zones = regions[regionIndex].Zones;
            for (var zoneIndex = 0; zoneIndex < zones.Count; zoneIndex++)
            {
                var zone = zones[zoneIndex];
                if (zone.Name.Contains(search, StringComparison.OrdinalIgnoreCase))
                {
                    searchResults.Add(zone);
                }
            }
        }
    }

    private bool DrawZoneRow(Rect row, MapZone zone)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var labelSize = Typography.Measure(zone.Name, TextStyles.Body);
        Typography.Draw(new Vector2(row.Min.X, row.Center.Y - labelSize.Y * 0.5f), zone.Name, frameTheme.TextStrong, TextStyles.Body);

        var countLabel = zone.Aetherytes.Count.ToString(Loc.Culture);
        var countSize = Typography.Measure(countLabel, TextStyles.Footnote);
        var chevronWidth = 6f * scale;
        var chevronTip = new Vector2(row.Max.X, row.Center.Y);
        DrawChevronRight(chevronTip, chevronWidth, 2.2f * scale, frameTheme.TextMuted);
        Typography.Draw(new Vector2(chevronTip.X - chevronWidth - 12f * scale - countSize.X, row.Center.Y - countSize.Y * 0.5f), countLabel, frameTheme.TextMuted, TextStyles.Footnote);

        var hovered = ImGui.IsMouseHoveringRect(row.Min, row.Max);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }

    private void DrawDetail(Rect area, MapZone zone)
    {
        var context = new PhoneContext(area, frameTheme, frameNavigation);
        AppHeader.Draw(context, zone.Name, backToList);

        var scale = ImGuiHelpers.GlobalScale;
        var top = area.Min.Y + AppHeader.Height * scale;
        var body = new Rect(new Vector2(area.Min.X, top), area.Max);

        using (AppSurface.Begin(body))
        {
            DrawMapPreview(zone);
            DrawAetherytes(zone);
            ImGui.Dummy(new Vector2(0f, 10f * scale));
        }
    }

    private void DrawMapPreview(MapZone zone)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var width = ImGui.GetContentRegionAvail().X;
        var origin = ImGui.GetCursorScreenPos();
        var height = MathF.Min(width, MapPreviewMaxHeight * scale);
        var rect = new Rect(origin, new Vector2(origin.X + width, origin.Y + height));
        var drawList = ImGui.GetWindowDrawList();
        var rounding = 16f * scale;

        if (textureCache.TryGetHandle(zone.MapTexturePath, out var handle))
        {
            Squircle.Fill(drawList, rect.Min, rect.Max, rounding, ImGui.GetColorU32(frameTheme.SurfaceMuted));
            drawList.AddImageRounded(handle, rect.Min, rect.Max, Vector2.Zero, Vector2.One, 0xFFFFFFFFu, rounding, ImDrawFlags.RoundCornersAll);
        }
        else
        {
            Squircle.Fill(drawList, rect.Min, rect.Max, rounding, ImGui.GetColorU32(frameTheme.GroupedCard));
            Typography.DrawCentered(rect.Center, Loc.T(L.Maps.MapUnavailable), frameTheme.TextMuted, TextStyles.Footnote);
        }

        Material.EdgeSquircle(drawList, rect.Min, rect.Max, rounding, scale);

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + 6f * scale));
    }

    private void DrawAetherytes(MapZone zone)
    {
        SettingsSection.Header(Loc.T(L.Maps.Aetherytes), frameTheme);

        var aetherytes = zone.Aetherytes;
        if (aetherytes.Count == 0)
        {
            var scale = ImGuiHelpers.GlobalScale;
            Typography.Draw(ImGui.GetCursorScreenPos() + new Vector2(4f * scale, 8f * scale), Loc.T(L.Maps.NoAetherytes), frameTheme.TextMuted, TextStyles.Footnote);
            ImGui.Dummy(new Vector2(ImGui.GetContentRegionAvail().X, 28f * scale));
            return;
        }

        var card = GroupCard.Begin(frameTheme, aetherytes.Count, AetheryteRowHeight);
        for (var index = 0; index < aetherytes.Count; index++)
        {
            DrawAetheryteRow(card.NextRow(), aetherytes[index]);
        }

        card.End();
    }

    private void DrawAetheryteRow(Rect row, MapAetheryte aetheryte)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var labelSize = Typography.Measure(aetheryte.Name, TextStyles.Body);
        Typography.Draw(new Vector2(row.Min.X, row.Center.Y - labelSize.Y * 0.5f), aetheryte.Name, frameTheme.TextStrong, TextStyles.Body);

        var buttonWidth = TravelButtonWidth * scale;
        var buttonHeight = 32f * scale;
        var buttonMin = new Vector2(row.Max.X - buttonWidth, row.Center.Y - buttonHeight * 0.5f);
        var buttonRect = new Rect(buttonMin, buttonMin + new Vector2(buttonWidth, buttonHeight));

        if (DrawTravelButton(buttonRect, Loc.T(L.Maps.Travel)))
        {
            if (lifestreamAvailable)
            {
                LifestreamBridge.TravelToAetheryte(aetheryte.Name);
            }
            else
            {
                ImGui.SetClipboardText(LifestreamBridge.AetheryteCommand(aetheryte.Name));
            }
        }

        if (!lifestreamAvailable && ImGui.IsMouseHoveringRect(buttonRect.Min, buttonRect.Max))
        {
            ImGui.SetTooltip(Loc.T(L.Maps.NeedsLifestream));
        }
    }

    private bool DrawTravelButton(Rect rect, string label)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        var hovered = ImGui.IsMouseHoveringRect(rect.Min, rect.Max);
        var rounding = rect.Height * 0.5f;

        var enabled = lifestreamAvailable;
        var fill = enabled ? frameTheme.Accent : frameTheme.GroupedCard;
        if (hovered && enabled)
        {
            fill = Palette.Mix(frameTheme.Accent, frameTheme.TextStrong, 0.12f);
        }

        var ink = enabled ? new Vector4(1f, 1f, 1f, 0.98f) : frameTheme.TextMuted;
        Squircle.Fill(drawList, rect.Min, rect.Max, rounding, ImGui.GetColorU32(fill));
        var textSize = Typography.Measure(label, TextStyles.FootnoteEmphasized);
        Typography.Draw(rect.Center - textSize * 0.5f, label, ink, TextStyles.FootnoteEmphasized);

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }

    private void DrawEmptyState(string message)
    {
        var scale = ImGuiHelpers.GlobalScale;
        Typography.Draw(ImGui.GetCursorScreenPos() + new Vector2(4f * scale, 16f * scale), message, frameTheme.TextMuted, TextStyles.Footnote);
        ImGui.Dummy(new Vector2(ImGui.GetContentRegionAvail().X, 40f * scale));
    }

    private static void DrawPin(ImDrawListPtr drawList, Vector2 center, float radius, Vector4 color)
    {
        var packed = ImGui.GetColorU32(color);
        var headCenter = new Vector2(center.X, center.Y - radius * 0.35f);
        drawList.AddCircleFilled(headCenter, radius * 0.7f, packed, 24);
        var tip = new Vector2(center.X, center.Y + radius);
        drawList.AddTriangleFilled(new Vector2(headCenter.X - radius * 0.55f, headCenter.Y + radius * 0.2f), new Vector2(headCenter.X + radius * 0.55f, headCenter.Y + radius * 0.2f), tip, packed);
        drawList.AddCircleFilled(headCenter, radius * 0.28f, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.95f)), 16);
    }

    private static void DrawChevronRight(Vector2 tip, float size, float thickness, Vector4 color)
    {
        var drawList = ImGui.GetWindowDrawList();
        var packed = ImGui.GetColorU32(color);
        drawList.AddLine(new Vector2(tip.X - size, tip.Y - size), tip, packed, thickness);
        drawList.AddLine(tip, new Vector2(tip.X - size, tip.Y + size), packed, thickness);
    }

    public void Dispose()
    {
    }
}
