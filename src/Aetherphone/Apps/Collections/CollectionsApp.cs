using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Collections;
using Aetherphone.Core.Game;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Lodestone;
using Aetherphone.Core.Net;
using Aetherphone.Core.Theme;
using Aetherphone.Windows;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Collections;

internal sealed class CollectionsApp : IPhoneApp
{
    private const float TilePadding = 16f;
    private const float TileGap = 12f;
    private const float TileHeight = 92f;
    private const float SearchHeight = 46f;
    private const float SegmentHeight = 34f;
    private const float ChipRowHeight = 36f;
    private const float RowHeight = 60f;
    private const float IconSize = 40f;
    private const int MaxRows = 120;

    public string Id => "collections";

    public string DisplayName => Loc.T(L.Apps.Collections);

    public string Glyph => "Co";

    public Vector4 Accent => new(0.36f, 0.62f, 0.96f, 1f);

    public int BadgeCount => 0;

    private readonly CollectionsCatalogService catalog;
    private readonly LodestoneService lodestone;
    private readonly MediaCache media;
    private readonly HttpService http;
    private readonly GameData gameData;

    private readonly ViewRouter<CollectionView> router;
    private readonly RouterDraw<CollectionView> drawView;
    private readonly Action back;

    private readonly List<CollectionItem> filtered = new();
    private readonly SortedSet<string> sourceSet = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> sourceList = new();
    private readonly Dictionary<string, float> iconFade = new();
    private readonly string[] ownershipLabels = new string[3];

    private string search = string.Empty;
    private OwnershipFilter ownership = OwnershipFilter.All;
    private int sourceIndex;
    private bool resetScroll;
    private string? lodestoneId;

    private PhoneTheme frameTheme = PhoneTheme.Default;
    private INavigator frameNavigation = null!;

    public CollectionsApp(CollectionsCatalogService catalog, LodestoneService lodestone, MediaCache media, HttpService http, GameData gameData)
    {
        this.catalog = catalog;
        this.lodestone = lodestone;
        this.media = media;
        this.http = http;
        this.gameData = gameData;

        router = new ViewRouter<CollectionView>(CollectionView.Root());
        drawView = DrawView;
        back = () => router.Pop();
    }

    public void OnOpened()
    {
        router.Reset();
        ResetFilters();
        ownershipLabels[0] = Loc.T(L.Collections.FilterAll);
        ownershipLabels[1] = Loc.T(L.Collections.FilterOwned);
        ownershipLabels[2] = Loc.T(L.Collections.FilterMissing);
        lodestoneId = ResolveLocalId();
        catalog.ResetOwned();
        for (var index = 0; index < CollectionCategories.All.Length; index++)
        {
            catalog.RequestCatalog(CollectionCategories.All[index]);
        }
    }

    public void OnClosed()
    {
        router.Reset();
        iconFade.Clear();
        ResetFilters();
    }

    public void Draw(in PhoneContext context)
    {
        frameTheme = context.Theme;
        frameNavigation = context.Navigation;
        router.Draw(context.Content, context.Theme.AppBackground, ImGui.GetIO().DeltaTime, drawView);
    }

    private void DrawView(CollectionView view, Rect area, int depth)
    {
        switch (view.Kind)
        {
            case CollectionViewKind.Category:
                DrawCategory(area, view.Category);
                break;
            case CollectionViewKind.Detail when view.Item is { } item:
                DrawDetail(area, view.Category, item);
                break;
            default:
                DrawRoot(area);
                break;
        }
    }

    private string? ResolveLocalId()
    {
        var player = gameData.LocalPlayer;
        if (player is null)
        {
            return null;
        }

        var name = player.Name.TextValue;
        var world = gameData.WorldName(player.HomeWorld.RowId);
        return lodestone.TryGetCachedId(name, world);
    }

    private void DrawRoot(Rect area)
    {
        var context = new PhoneContext(area, frameTheme, frameNavigation);
        AppHeader.Draw(context, DisplayName);

        var scale = ImGuiHelpers.GlobalScale;
        var body = new Rect(new Vector2(area.Min.X, area.Min.Y + AppHeader.Height * scale), area.Max);

        using (AppSurface.Begin(body))
        {
            if (lodestoneId is null)
            {
                DrawLinkHint();
            }

            ImGui.Dummy(new Vector2(0f, 4f * scale));
            DrawCategoryTiles();
        }
    }

    private void DrawLinkHint()
    {
        var scale = ImGuiHelpers.GlobalScale;
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var height = 34f * scale;
        var drawList = ImGui.GetWindowDrawList();
        var min = origin;
        var max = new Vector2(origin.X + width, origin.Y + height);
        Squircle.Fill(drawList, min, max, 10f * scale, ImGui.GetColorU32(Palette.WithAlpha(frameTheme.Accent, 0.12f)));
        Typography.Draw(new Vector2(min.X + 14f * scale, min.Y + (height - Typography.Measure(Loc.T(L.Collections.LinkHint), 0.8f).Y) * 0.5f), Loc.T(L.Collections.LinkHint), frameTheme.TextMuted, 0.8f, FontWeight.Medium);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height + 10f * scale));
    }

    private void DrawCategoryTiles()
    {
        var scale = ImGuiHelpers.GlobalScale;
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var gap = TileGap * scale;
        var columns = 2;
        var tileWidth = (width - gap) / columns;
        var tileHeight = TileHeight * scale;

        for (var index = 0; index < CollectionCategories.All.Length; index++)
        {
            var category = CollectionCategories.All[index];
            var column = index % columns;
            var rowIndex = index / columns;
            var min = new Vector2(origin.X + column * (tileWidth + gap), origin.Y + rowIndex * (tileHeight + gap));
            var max = new Vector2(min.X + tileWidth, min.Y + tileHeight);
            if (DrawTile(new Rect(min, max), category, scale))
            {
                OpenCategory(category);
            }
        }

        var rows = (CollectionCategories.All.Length + columns - 1) / columns;
        var totalHeight = rows * tileHeight + (rows - 1) * gap;
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, totalHeight + 12f * scale));
    }

    private bool DrawTile(Rect rect, CollectionCategory category, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var rounding = 16f * scale;
        var hovered = ImGui.IsMouseHoveringRect(rect.Min, rect.Max);

        Elevation.Card(drawList, rect.Min, rect.Max, rounding, scale, 0.5f);
        Squircle.Fill(drawList, rect.Min, rect.Max, rounding, ImGui.GetColorU32(frameTheme.GroupedCard));
        if (hovered)
        {
            Squircle.Fill(drawList, rect.Min, rect.Max, rounding, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.04f)));
        }

        var entry = catalog.RequestCatalog(category);
        var owned = lodestoneId is not null ? catalog.RequestOwned(lodestoneId, category) : null;
        var total = entry.Total;

        Typography.Draw(new Vector2(rect.Min.X + 14f * scale, rect.Min.Y + 13f * scale), CategoryLabel(category), frameTheme.TextStrong, 0.98f, FontWeight.SemiBold);

        var countLabel = total > 0 ? total.ToString() : "—";
        Typography.Draw(new Vector2(rect.Min.X + 14f * scale, rect.Min.Y + 36f * scale), countLabel, frameTheme.TextMuted, 0.8f, FontWeight.Medium);

        DrawTileRing(rect, owned, total, scale);

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }

    private void DrawTileRing(Rect rect, OwnedEntry? owned, int total, float scale)
    {
        var center = new Vector2(rect.Max.X - 30f * scale, rect.Max.Y - 30f * scale);
        var radius = 18f * scale;
        var thickness = 4f * scale;

        if (owned is not { State: OwnedState.Ready } || total <= 0)
        {
            ProgressRing.Track(center, radius, thickness, Palette.WithAlpha(frameTheme.TextMuted, 0.25f));
            return;
        }

        var fraction = Math.Clamp(owned.Count / (float)total, 0f, 1f);
        ProgressRing.Track(center, radius, thickness, Palette.WithAlpha(frameTheme.TextMuted, 0.22f));
        ProgressRing.Fill(center, radius, thickness, fraction, frameTheme.Accent);
        var percent = (int)MathF.Round(fraction * 100f);
        Typography.DrawCentered(center, percent + "%", frameTheme.TextStrong, 0.68f, FontWeight.SemiBold);
    }

    private void OpenCategory(CollectionCategory category)
    {
        ResetFilters();
        resetScroll = true;
        catalog.RequestCatalog(category);
        if (lodestoneId is not null)
        {
            catalog.RequestOwned(lodestoneId, category);
        }

        router.Push(CollectionView.ForCategory(category));
    }

    private void DrawCategory(Rect area, CollectionCategory category)
    {
        var context = new PhoneContext(area, frameTheme, frameNavigation);
        AppHeader.Draw(context, CategoryLabel(category), back);

        var scale = ImGuiHelpers.GlobalScale;
        var pad = 16f * scale;
        var top = area.Min.Y + AppHeader.Height * scale;

        var entry = catalog.RequestCatalog(category);
        var owned = lodestoneId is not null ? catalog.RequestOwned(lodestoneId, category) : null;

        var searchBar = new Rect(new Vector2(area.Min.X + pad, top), new Vector2(area.Max.X - pad, top + SearchHeight * scale));
        SearchField.Draw(searchBar, "##collectSearch", Loc.T(L.Collections.Search), ref search, frameTheme, 60);

        var hasOwned = owned is { State: OwnedState.Ready };
        var segmentBar = new Rect(new Vector2(area.Min.X + pad, searchBar.Max.Y), new Vector2(area.Max.X - pad, searchBar.Max.Y + SegmentHeight * scale));
        if (hasOwned)
        {
            DrawOwnershipSegments(segmentBar);
        }

        var chipTop = hasOwned ? segmentBar.Max.Y + 2f * scale : searchBar.Max.Y;
        var chipBar = new Rect(new Vector2(area.Min.X + pad, chipTop), new Vector2(area.Max.X - pad, chipTop + ChipRowHeight * scale));
        DrawSourceChip(entry, chipBar);

        var body = new Rect(new Vector2(area.Min.X, chipBar.Max.Y), area.Max);

        if (entry.State == CollectionState.Failed)
        {
            DrawFailed(body, category);
            return;
        }

        if (entry.State != CollectionState.Ready)
        {
            DrawSpinnerState(body);
            return;
        }

        var sourceFilter = sourceIndex > 0 && sourceIndex <= sourceList.Count ? sourceList[sourceIndex - 1] : string.Empty;
        CollectionFilter.Apply(entry.Items, filtered, search, ownership, sourceFilter, owned);

        using (AppSurface.Begin(body))
        {
            if (resetScroll)
            {
                ImGui.SetScrollY(0f);
                resetScroll = false;
            }

            DrawSummary(entry, owned);
            DrawOwnedNotice(owned);

            if (filtered.Count == 0)
            {
                Typography.Draw(ImGui.GetCursorScreenPos() + new Vector2(4f * scale, 14f * scale), Loc.T(L.Collections.NoResults), frameTheme.TextMuted, 0.85f);
                ImGui.Dummy(new Vector2(ImGui.GetContentRegionAvail().X, 40f * scale));
                return;
            }

            DrawList(category, owned);
        }
    }

    private void DrawSummary(CatalogEntry entry, OwnedEntry? owned)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var origin = ImGui.GetCursorScreenPos();
        string summary;
        if (owned is { State: OwnedState.Ready } && entry.Total > 0)
        {
            var percent = (int)MathF.Round(owned.Count / (float)entry.Total * 100f);
            summary = $"{owned.Count} / {entry.Total}  ·  {Loc.T(L.Collections.CompletePercent, percent)}";
        }
        else
        {
            summary = entry.Total.ToString();
        }

        Typography.Draw(new Vector2(origin.X + 4f * scale, origin.Y + 8f * scale), summary, frameTheme.TextMuted, 0.78f);
        ImGui.Dummy(new Vector2(ImGui.GetContentRegionAvail().X, 28f * scale));
    }

    private void DrawOwnedNotice(OwnedEntry? owned)
    {
        var message = owned?.State switch
        {
            OwnedState.Private => Loc.T(L.Collections.CollectionPrivate),
            OwnedState.Failed => Loc.T(L.Collections.OwnedUnavailable),
            _ => string.Empty,
        };

        if (message.Length == 0)
        {
            return;
        }

        var scale = ImGuiHelpers.GlobalScale;
        Typography.Draw(ImGui.GetCursorScreenPos() + new Vector2(4f * scale, 2f * scale), message, frameTheme.TextMuted, 0.76f);
        ImGui.Dummy(new Vector2(ImGui.GetContentRegionAvail().X, 22f * scale));
    }

    private void DrawList(CollectionCategory category, OwnedEntry? owned)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var count = Math.Min(filtered.Count, MaxRows);
        var card = GroupCard.Begin(frameTheme, count, RowHeight);
        var hasOwned = owned is { State: OwnedState.Ready };

        for (var index = 0; index < count; index++)
        {
            var item = filtered[index];
            var row = card.NextRow();
            var hovered = ImGui.IsMouseHoveringRect(new Vector2(row.Min.X - 16f * scale, row.Min.Y), new Vector2(row.Max.X + 16f * scale, row.Max.Y));
            DrawRow(row, item, hasOwned && owned!.Ids.Contains(item.Id), hasOwned, scale);

            if (hovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                {
                    OpenItem(category, item);
                }
            }
        }

        card.End();

        if (filtered.Count > MaxRows)
        {
            Typography.Draw(ImGui.GetCursorScreenPos() + new Vector2(4f * scale, 6f * scale), Loc.T(L.Collections.MoreCount, filtered.Count - MaxRows), frameTheme.TextMuted, 0.8f);
            ImGui.Dummy(new Vector2(ImGui.GetContentRegionAvail().X, 26f * scale));
        }
    }

    private void DrawRow(Rect row, CollectionItem item, bool isOwned, bool hasOwned, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var iconBox = IconSize * scale;
        var iconMin = new Vector2(row.Min.X, row.Center.Y - iconBox * 0.5f);
        var iconMax = iconMin + new Vector2(iconBox, iconBox);
        DrawIcon(drawList, item, iconMin, iconMax, 9f * scale);

        var textLeft = iconMax.X + 12f * scale;
        var nameY = row.Min.Y + 12f * scale;
        Typography.Draw(new Vector2(textLeft, nameY), item.Name, frameTheme.TextStrong, 0.92f, FontWeight.Medium);

        var subtitle = SubtitleOf(item);
        if (subtitle.Length > 0)
        {
            Typography.Draw(new Vector2(textLeft, nameY + 22f * scale), subtitle, frameTheme.TextMuted, 0.78f);
        }

        if (hasOwned)
        {
            DrawOwnedDot(drawList, new Vector2(row.Max.X - 4f * scale, row.Center.Y), isOwned, scale);
        }
    }

    private void DrawOwnedDot(ImDrawListPtr drawList, Vector2 center, bool isOwned, float scale)
    {
        var radius = 6f * scale;
        if (isOwned)
        {
            drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(frameTheme.ToggleOn), 16);
            return;
        }

        drawList.AddCircle(center, radius, ImGui.GetColorU32(Palette.WithAlpha(frameTheme.TextMuted, 0.5f)), 16, 1.6f * scale);
    }

    private void DrawDetail(Rect area, CollectionCategory category, CollectionItem item)
    {
        var context = new PhoneContext(area, frameTheme, frameNavigation);
        AppHeader.Draw(context, item.Name, back);

        var scale = ImGuiHelpers.GlobalScale;
        var body = new Rect(new Vector2(area.Min.X, area.Min.Y + AppHeader.Height * scale), area.Max);

        using (AppSurface.Begin(body))
        {
            DrawDetailHero(item, category);
            DrawDetailDescription(item);
            DrawDetailInfo(item, category);
            DrawDetailSources(item);
        }
    }

    private void DrawDetailHero(CollectionItem item, CollectionCategory category)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var iconBox = 72f * scale;
        var iconMin = origin;
        var iconMax = iconMin + new Vector2(iconBox, iconBox);
        DrawIcon(drawList, item, iconMin, iconMax, 14f * scale);

        var textLeft = iconMax.X + 14f * scale;
        Typography.Draw(new Vector2(textLeft, origin.Y + 6f * scale), item.Name, frameTheme.TextStrong, 1.05f, FontWeight.SemiBold);

        var owned = lodestoneId is not null ? catalog.RequestOwned(lodestoneId, category) : null;
        if (owned is { State: OwnedState.Ready })
        {
            var isOwned = owned.Ids.Contains(item.Id);
            var label = isOwned ? Loc.T(L.Collections.Owned) : Loc.T(L.Collections.Missing);
            var color = isOwned ? frameTheme.ToggleOn : frameTheme.TextMuted;
            Typography.Draw(new Vector2(textLeft, origin.Y + 32f * scale), label, color, 0.82f, FontWeight.SemiBold);
        }

        if (item.Stars > 0)
        {
            Typography.Draw(new Vector2(textLeft, origin.Y + 52f * scale), new string('★', Math.Clamp(item.Stars, 1, 5)), frameTheme.Accent, 0.82f, FontWeight.Medium);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(ImGui.GetContentRegionAvail().X, iconBox + 14f * scale));
    }

    private void DrawDetailDescription(CollectionItem item)
    {
        if (item.Description.Length == 0)
        {
            return;
        }

        SettingsSection.Header(Loc.T(L.Collections.About), frameTheme);
        var scale = ImGuiHelpers.GlobalScale;
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 4f * scale);
        using (Plugin.Fonts.Push(0.88f))
        using (ImRaii.PushColor(ImGuiCol.Text, frameTheme.TextStrong))
        {
            ImGui.PushTextWrapPos(0f);
            ImGui.TextUnformatted(item.Description);
            ImGui.PopTextWrapPos();
        }

        ImGui.Dummy(new Vector2(0f, 10f * scale));
    }

    private void DrawDetailInfo(CollectionItem item, CollectionCategory category)
    {
        var rows = 0;
        if (item.Patch.Length > 0)
        {
            rows++;
        }

        if (item.HasTradeable)
        {
            rows++;
        }

        if (category == CollectionCategory.Achievements && item.Points > 0)
        {
            rows++;
        }

        if (item.Community.Length > 0)
        {
            rows++;
        }

        if (category == CollectionCategory.TriadCards && item.Stats is not null)
        {
            rows++;
        }

        if (rows == 0)
        {
            return;
        }

        SettingsSection.Header(Loc.T(L.Collections.Details), frameTheme);
        var card = GroupCard.Begin(frameTheme, rows);

        if (item.Patch.Length > 0)
        {
            SettingsRow.Info(card.NextRow(), Loc.T(L.Collections.Patch), item.Patch, frameTheme);
        }

        if (category == CollectionCategory.Achievements && item.Points > 0)
        {
            SettingsRow.Info(card.NextRow(), Loc.T(L.Collections.Points), item.Points.ToString(), frameTheme);
        }

        if (category == CollectionCategory.TriadCards && item.Stats is { } stats)
        {
            SettingsRow.Info(card.NextRow(), Loc.T(L.Collections.CardStats), $"{stats.Top} · {stats.Right} · {stats.Bottom} · {stats.Left}", frameTheme);
        }

        if (item.HasTradeable)
        {
            SettingsRow.Info(card.NextRow(), Loc.T(L.Collections.Tradeable), item.Tradeable ? Loc.T(L.Collections.Yes) : Loc.T(L.Collections.No), frameTheme);
        }

        if (item.Community.Length > 0)
        {
            SettingsRow.Info(card.NextRow(), Loc.T(L.Collections.Community), item.Community, frameTheme);
        }

        card.End();
    }

    private void DrawDetailSources(CollectionItem item)
    {
        if (item.Sources.Length == 0)
        {
            return;
        }

        SettingsSection.Header(Loc.T(L.Collections.HowToObtain), frameTheme);
        var card = GroupCard.Begin(frameTheme, item.Sources.Length, 52f);
        for (var index = 0; index < item.Sources.Length; index++)
        {
            var source = item.Sources[index];
            var row = card.NextRow();
            var scale = ImGuiHelpers.GlobalScale;
            var type = source.Type ?? string.Empty;
            var text = source.Text ?? string.Empty;
            Typography.Draw(new Vector2(row.Min.X, row.Min.Y + 9f * scale), type.Length > 0 ? type : Loc.T(L.Collections.Source), frameTheme.TextStrong, 0.86f, FontWeight.Medium);
            if (text.Length > 0)
            {
                Typography.Draw(new Vector2(row.Min.X, row.Min.Y + 28f * scale), text, frameTheme.TextMuted, 0.78f);
            }
        }

        card.End();
    }

    private void DrawIcon(ImDrawListPtr drawList, CollectionItem item, Vector2 min, Vector2 max, float rounding)
    {
        var scale = ImGuiHelpers.GlobalScale;
        drawList.AddRectFilled(min, max, ImGui.GetColorU32(frameTheme.SurfaceMuted), rounding);

        if (item.IconUrl.Length == 0)
        {
            return;
        }

        var result = Thumb(item.IconUrl);
        if (result.Texture is { } texture)
        {
            var fade = StepFade(item.IconUrl, true);
            var tint = ImGui.GetColorU32(new Vector4(1f, 1f, 1f, fade));
            drawList.AddImageRounded(texture.Handle, min, max, Vector2.Zero, Vector2.One, tint, rounding, ImDrawFlags.RoundCornersAll);
            return;
        }

        StepFade(item.IconUrl, false);
        if (result.Loading)
        {
            ProgressRing.Sweep((min + max) * 0.5f, 9f * scale, 2f * scale, frameTheme.TextMuted, 900.0, 1.8f, 0.9f);
        }
    }

    private void DrawOwnershipSegments(Rect bar)
    {
        var labels = new[]
        {
            Loc.T(L.Collections.FilterAll),
            Loc.T(L.Collections.FilterOwned),
            Loc.T(L.Collections.FilterMissing),
        };

        var selected = SegmentStrip.Draw("collections.ownership", bar, labels, (int)ownership, frameTheme);
        if (selected != (int)ownership)
        {
            ownership = (OwnershipFilter)selected;
            resetScroll = true;
        }
    }

    private void DrawSourceChip(CatalogEntry entry, Rect bar)
    {
        if (entry.State != CollectionState.Ready)
        {
            return;
        }

        CollectionFilter.CollectSourceTypes(entry.Items, sourceSet);
        sourceList.Clear();
        foreach (var type in sourceSet)
        {
            sourceList.Add(type);
        }

        if (sourceIndex > sourceList.Count)
        {
            sourceIndex = 0;
        }

        var scale = ImGuiHelpers.GlobalScale;
        var label = sourceIndex == 0 ? Loc.T(L.Collections.AllSources) : sourceList[sourceIndex - 1];
        var cursor = bar.Min.X;
        if (DrawChip(ref cursor, bar.Center.Y, 8f * scale, label, sourceIndex != 0))
        {
            sourceIndex = sourceList.Count == 0 ? 0 : (sourceIndex + 1) % (sourceList.Count + 1);
            resetScroll = true;
        }
    }

    private bool DrawChip(ref float cursorX, float centerY, float gap, string label, bool active)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        var textSize = Typography.Measure(label, 0.8f, FontWeight.Medium);
        var height = 28f * scale;
        var width = textSize.X + 22f * scale;
        var min = new Vector2(cursorX, centerY - height * 0.5f);
        var max = new Vector2(cursorX + width, centerY + height * 0.5f);

        var hovered = ImGui.IsMouseHoveringRect(min, max);
        var fill = active ? Palette.WithAlpha(frameTheme.Accent, 0.92f) : frameTheme.GroupedCard;
        Squircle.Fill(drawList, min, max, height * 0.5f, ImGui.GetColorU32(fill));
        var ink = active || hovered ? frameTheme.TextStrong : frameTheme.TextMuted;
        Typography.Draw(new Vector2(min.X + (width - textSize.X) * 0.5f, centerY - textSize.Y * 0.5f), label, ink, 0.8f, FontWeight.Medium);

        cursorX = max.X + gap;
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }

    private void DrawFailed(Rect body, CollectionCategory category)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var center = body.Center;
        ProgressRing.CenterIcon(new Vector2(center.X, center.Y - 24f * scale), FontAwesomeIcon.CloudDownloadAlt, frameTheme.TextMuted, 32f * scale);
        Typography.DrawCentered(new Vector2(center.X, center.Y + 18f * scale), Loc.T(L.Collections.Failed), frameTheme.TextMuted, 0.92f, FontWeight.Medium);
        if (DrawTextButton(new Vector2(center.X, center.Y + 50f * scale), Loc.T(L.Collections.TryAgain), scale))
        {
            catalog.Retry(category);
        }
    }

    private bool DrawTextButton(Vector2 center, string label, float scale)
    {
        var size = Typography.Measure(label, 0.9f, FontWeight.SemiBold);
        var hitMin = new Vector2(center.X - size.X * 0.5f - 12f * scale, center.Y - size.Y * 0.5f - 6f * scale);
        var hitMax = new Vector2(center.X + size.X * 0.5f + 12f * scale, center.Y + size.Y * 0.5f + 6f * scale);
        var hovered = ImGui.IsMouseHoveringRect(hitMin, hitMax);

        var drawList = ImGui.GetWindowDrawList();
        drawList.AddRectFilled(hitMin, hitMax, ImGui.GetColorU32(Palette.WithAlpha(Accent, hovered ? 0.22f : 0.14f)), (hitMax.Y - hitMin.Y) * 0.5f);
        Typography.DrawCentered(center, label, Accent, 0.9f, FontWeight.SemiBold);

        if (!hovered)
        {
            return false;
        }

        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        return ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }

    private void DrawSpinnerState(Rect body)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var center = body.Center;
        ProgressRing.Sweep(new Vector2(center.X, center.Y - 6f * scale), 13f * scale, 2.4f * scale, Accent, 900.0, 1.8f, 0.95f);
        Typography.DrawCentered(new Vector2(center.X, center.Y + 24f * scale), Loc.T(L.Common.Loading), frameTheme.TextMuted, 0.9f);
    }

    private void OpenItem(CollectionCategory category, CollectionItem item) => router.Push(CollectionView.ForItem(category, item));

    private string SubtitleOf(CollectionItem item)
    {
        if (item.SourceType.Length > 0 && item.SourceText.Length > 0)
        {
            return $"{item.SourceType} · {item.SourceText}";
        }

        if (item.SourceText.Length > 0)
        {
            return item.SourceText;
        }

        return item.SourceType;
    }

    private MediaResult Thumb(string url) => media.GetOrRequest(url, token => http.GetBytesAsync(new Uri(url), token));

    private float StepFade(string url, bool ready)
    {
        iconFade.TryGetValue(url, out var fade);
        var target = ready ? 1f : 0f;
        if (fade < target)
        {
            fade = Math.Min(target, fade + ImGui.GetIO().DeltaTime / 0.22f);
        }

        iconFade[url] = fade;
        return fade;
    }

    private void ResetFilters()
    {
        search = string.Empty;
        ownership = OwnershipFilter.All;
        sourceIndex = 0;
    }

    private static string CategoryLabel(CollectionCategory category) => category switch
    {
        CollectionCategory.Mounts => Loc.T(L.Collections.Mounts),
        CollectionCategory.Minions => Loc.T(L.Collections.Minions),
        CollectionCategory.Emotes => Loc.T(L.Collections.Emotes),
        CollectionCategory.Orchestrions => Loc.T(L.Collections.Orchestrions),
        CollectionCategory.Hairstyles => Loc.T(L.Collections.Hairstyles),
        CollectionCategory.Facewear => Loc.T(L.Collections.Facewear),
        CollectionCategory.Achievements => Loc.T(L.Collections.Achievements),
        _ => Loc.T(L.Collections.TriadCards),
    };

    public void Dispose()
    {
    }
}
