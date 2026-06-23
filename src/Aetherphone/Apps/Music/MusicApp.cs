using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Radio;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Plugin.Services;

namespace Aetherphone.Apps.Music;

internal sealed class MusicApp : IPhoneApp
{
    private enum View : byte
    {
        Browse,
        Stations,
        NowPlaying,
    }

    private const float MiniHeight = 58f;
    private const float TileHeight = 92f;

    public string Id => "music";

    public string DisplayName => "Music";

    public string Glyph => "M";

    public Vector4 Accent => new(0.96f, 0.36f, 0.52f, 1f);

    public int BadgeCount => 0;

    private readonly RadioService radio;
    private readonly RadioPlayer player;
    private readonly ArtworkCache artwork;

    private View view = View.Browse;
    private View returnView = View.Browse;
    private int categoryIndex = -1;
    private RadioStation[] stations = Array.Empty<RadioStation>();
    private volatile bool loading;
    private CancellationTokenSource? fetch;
    private float clock;

    public MusicApp(RadioService radio, RadioPlayer player, ITextureProvider textures)
    {
        this.radio = radio;
        this.player = player;
        artwork = new ArtworkCache(textures);
    }

    public void OnOpened()
    {
    }

    public void OnClosed()
    {
    }

    public void Draw(in PhoneContext context)
    {
        clock += MathF.Min(ImGui.GetIO().DeltaTime, 0.1f);

        if (view == View.NowPlaying && player.State == RadioPlaybackState.Stopped)
        {
            view = returnView;
        }

        switch (view)
        {
            case View.Stations:
                DrawStations(context);
                break;
            case View.NowPlaying:
                DrawNowPlaying(context);
                break;
            default:
                DrawBrowse(context);
                break;
        }
    }

    private void DrawBrowse(in PhoneContext context)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var theme = context.Theme;
        var content = context.Content;

        AppHeader.Draw(context, DisplayName);

        var body = ScrollBody(content, scale);
        using (AppSurface.Begin(body))
        {
            ImGui.Dummy(new Vector2(0f, 4f * scale));
            DrawCategoryGrid(theme, scale);
        }

        DrawMiniPlayer(context, scale);
    }

    private void DrawCategoryGrid(PhoneTheme theme, float scale)
    {
        var categories = RadioService.Categories;
        var gap = 10f * scale;
        var available = ImGui.GetContentRegionAvail().X;
        var tileWidth = (available - gap) * 0.5f;
        var tileHeight = TileHeight * scale;
        var origin = ImGui.GetCursorScreenPos();
        var rows = (categories.Length + 1) / 2;
        var dl = ImGui.GetWindowDrawList();

        for (var index = 0; index < categories.Length; index++)
        {
            var column = index % 2;
            var row = index / 2;
            var min = new Vector2(origin.X + column * (tileWidth + gap), origin.Y + row * (tileHeight + gap));
            var max = min + new Vector2(tileWidth, tileHeight);
            var rounding = 16f * scale;

            var hovered = ImGui.IsMouseHoveringRect(min, max);
            var seed = ArtGradient.Seed(categories[index].Tag);
            dl.AddImageRounded(artwork.Handle(seed), min, max, Vector2.Zero, Vector2.One, 0xFFFFFFFFu, rounding, ImDrawFlags.RoundCornersAll);
            dl.AddRectFilledMultiColor(new Vector2(min.X, max.Y - tileHeight * 0.6f), max, 0u, 0u, 0x66000000u, 0x66000000u);
            if (hovered)
            {
                dl.AddRectFilled(min, max, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.12f)), rounding);
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            var label = categories[index].Display;
            var labelPosition = new Vector2(min.X + 12f * scale, max.Y - 26f * scale);
            Typography.Draw(labelPosition + new Vector2(1f, 1f), label, new Vector4(0f, 0f, 0f, 0.5f), 1.0f);
            Typography.Draw(labelPosition, label, new Vector4(1f, 1f, 1f, 1f), 1.0f);

            if (hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            {
                OpenCategory(index);
            }
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(available, rows * tileHeight + (rows - 1) * gap + 8f * scale));
    }

    private void DrawStations(in PhoneContext context)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var theme = context.Theme;
        var content = context.Content;

        AppHeader.Draw(context, CategoryTitle(), GoToBrowse);

        var body = ScrollBody(content, scale);
        if (loading)
        {
            Typography.DrawCentered(body.Center, "Tuning in…", theme.TextMuted);
            DrawMiniPlayer(context, scale);
            return;
        }

        if (stations.Length == 0)
        {
            Typography.DrawCentered(body.Center, "No stations found", theme.TextMuted);
            DrawMiniPlayer(context, scale);
            return;
        }

        using (AppSurface.Begin(body))
        {
            ImGui.Dummy(new Vector2(0f, 2f * scale));
            for (var index = 0; index < stations.Length; index++)
            {
                DrawStationRow(theme, scale, stations[index], index);
            }
        }

        DrawMiniPlayer(context, scale);
    }

    private void DrawStationRow(PhoneTheme theme, float scale, RadioStation station, int index)
    {
        var rowHeight = 68f * scale;
        var width = ImGui.GetContentRegionAvail().X;
        var origin = ImGui.GetCursorScreenPos();
        var min = origin;
        var max = new Vector2(origin.X + width, origin.Y + rowHeight);
        var dl = ImGui.GetWindowDrawList();

        var playing = IsCurrent(station);
        var hovered = ImGui.IsMouseHoveringRect(min, max);
        if (hovered || playing)
        {
            dl.AddRectFilled(min, max, ImGui.GetColorU32(Palette.WithAlpha(playing ? Accent : theme.TextStrong, playing ? 0.10f : 0.06f)), 14f * scale);
        }

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var discRadius = 25f * scale;
        var discCenter = new Vector2(min.X + 10f * scale + discRadius, min.Y + rowHeight * 0.5f);
        dl.AddCircleFilled(discCenter + new Vector2(0f, 2.5f * scale), discRadius, ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.30f)), 48);
        ArtGradient.DrawDisc(dl, discCenter, discRadius, ArtGradient.FromName(station.Name), 1f);

        var trailing = playing ? 26f * scale : 12f * scale;
        var textLeft = discCenter.X + discRadius + 14f * scale;
        var nameColor = playing ? Accent : theme.TextStrong;
        Typography.Draw(new Vector2(textLeft, min.Y + 14f * scale), Truncate(station.Name, 22), nameColor, 1.2f);
        Typography.Draw(new Vector2(textLeft, min.Y + 41f * scale), Subtitle(station), Palette.WithAlpha(theme.TextStrong, 0.62f), 0.8f);

        if (playing)
        {
            Equalizer.Draw(dl, new Vector2(max.X - trailing, discCenter.Y), scale, 17f * scale, clock, Accent, 1f, player.State == RadioPlaybackState.Playing);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, rowHeight));

        if (hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            player.Play(stations, index);
        }
    }

    private void DrawNowPlaying(in PhoneContext context)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var theme = context.Theme;
        var content = context.Content;
        var state = player.State;
        var swatch = ArtGradient.FromName(player.CurrentStation);
        var dl = ImGui.GetWindowDrawList();

        var tintTop = ImGui.GetColorU32(Palette.WithAlpha(swatch.Top, 0.42f));
        var tintBottom = ImGui.GetColorU32(Palette.WithAlpha(swatch.Top, 0f));
        dl.AddRectFilledMultiColor(content.Min, new Vector2(content.Max.X, content.Min.Y + content.Height * 0.6f), tintTop, tintTop, tintBottom, tintBottom);

        AppHeader.Draw(context, "Now Playing", GoToReturnView);

        var body = new Rect(new Vector2(content.Min.X, content.Min.Y + AppHeader.Height * scale), content.Max);
        var centerX = body.Center.X;

        var artSize = MathF.Min(body.Width * 0.66f, 196f * scale);
        var artTop = body.Min.Y + 14f * scale;
        var artMin = new Vector2(centerX - artSize * 0.5f, artTop);
        var artMax = artMin + new Vector2(artSize, artSize);
        var artRounding = 20f * scale;

        dl.AddRectFilled(artMin + new Vector2(0f, 10f * scale), artMax + new Vector2(0f, 12f * scale), ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.35f)), artRounding);
        dl.AddImageRounded(artwork.HandleForName(player.CurrentStation), artMin, artMax, Vector2.Zero, Vector2.One, 0xFFFFFFFFu, artRounding, ImDrawFlags.RoundCornersAll);
        var pulse = state == RadioPlaybackState.Buffering ? 0.5f + 0.5f * MathF.Abs(MathF.Sin(clock * 3f)) : 1f;
        Equalizer.Draw(dl, new Vector2(artMax.X - 22f * scale, artMax.Y - 18f * scale), scale, 16f * scale, clock, new Vector4(1f, 1f, 1f, 1f), pulse, state == RadioPlaybackState.Playing);

        var nameY = artMax.Y + 30f * scale;
        Typography.DrawCentered(new Vector2(centerX, nameY), Truncate(player.CurrentStation, 22), theme.TextStrong, 1.45f);
        Typography.DrawCentered(new Vector2(centerX, nameY + 26f * scale), NowPlayingSubtitle(state), Palette.WithAlpha(Accent, 0.95f), 0.9f);

        var controlsY = nameY + 70f * scale;
        var active = true;
        if (player.HasQueue)
        {
            if (TransportButton.Draw(new Vector2(centerX - 64f * scale, controlsY), 22f * scale, TransportAction.Previous, Accent, theme.TextStrong, 1f, active))
            {
                player.Previous();
            }

            if (TransportButton.Draw(new Vector2(centerX + 64f * scale, controlsY), 22f * scale, TransportAction.Next, Accent, theme.TextStrong, 1f, active))
            {
                player.Next();
            }
        }

        if (TransportButton.Draw(new Vector2(centerX, controlsY), 30f * scale, TransportAction.Stop, Accent, theme.TextStrong, 1f, active))
        {
            player.Stop();
            GoToReturnView();
        }

        var trackY = controlsY + 52f * scale;
        var trackRect = new Rect(new Vector2(body.Min.X + 28f * scale, trackY - 3f * scale), new Vector2(body.Max.X - 28f * scale, trackY + 3f * scale));
        player.Volume = Scrubber.Draw(trackRect, player.Volume, Accent, Palette.WithAlpha(theme.TextStrong, 0.18f), 1f);
    }

    private void DrawMiniPlayer(in PhoneContext context, float scale)
    {
        if (player.State == RadioPlaybackState.Stopped)
        {
            return;
        }

        var theme = context.Theme;
        var content = context.Content;
        var height = MiniHeight * scale;
        var min = new Vector2(content.Min.X + 2f * scale, content.Max.Y - height);
        var max = new Vector2(content.Max.X - 2f * scale, content.Max.Y - 2f * scale);
        var rounding = 16f * scale;
        var dl = ImGui.GetWindowDrawList();

        var hovered = ImGui.IsMouseHoveringRect(min, max);
        dl.AddRectFilled(min, max, ImGui.GetColorU32(theme.Surface), rounding);
        dl.AddRect(min, max, ImGui.GetColorU32(Palette.WithAlpha(theme.TextStrong, 0.08f)), rounding, ImDrawFlags.RoundCornersAll, 1f);

        var discRadius = 18f * scale;
        var discCenter = new Vector2(min.X + 12f * scale + discRadius, min.Y + height * 0.5f);
        ArtGradient.DrawDisc(dl, discCenter, discRadius, ArtGradient.FromName(player.CurrentStation), 1f);

        var textLeft = discCenter.X + discRadius + 12f * scale;
        var stopCenter = new Vector2(max.X - 26f * scale, discCenter.Y);
        Typography.Draw(new Vector2(textLeft, min.Y + 11f * scale), Truncate(player.CurrentStation, 18), theme.TextStrong, 0.95f);
        Typography.Draw(new Vector2(textLeft, min.Y + 31f * scale), StateLabel(player.State), theme.TextMuted, 0.8f);

        var stopped = TransportButton.Draw(stopCenter, 16f * scale, TransportAction.Stop, Accent, theme.TextStrong, 1f, true);
        if (stopped)
        {
            player.Stop();
            return;
        }

        if (hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left) && !ImGui.IsMouseHoveringRect(stopCenter - new Vector2(16f * scale, 16f * scale), stopCenter + new Vector2(16f * scale, 16f * scale)))
        {
            returnView = view;
            view = View.NowPlaying;
        }

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }
    }

    private Rect ScrollBody(Rect content, float scale)
    {
        var top = content.Min.Y + AppHeader.Height * scale;
        var bottom = content.Max.Y - (player.State == RadioPlaybackState.Stopped ? 0f : MiniHeight * scale + 4f * scale);
        return new Rect(new Vector2(content.Min.X, top), new Vector2(content.Max.X, bottom));
    }

    private void OpenCategory(int index)
    {
        categoryIndex = index;
        view = View.Stations;
        BeginFetch(RadioService.Categories[index].Tag);
    }

    private void BeginFetch(string tag)
    {
        fetch?.Cancel();
        fetch?.Dispose();
        fetch = new CancellationTokenSource();
        var token = fetch.Token;
        loading = true;
        stations = Array.Empty<RadioStation>();
        _ = FetchAsync(tag, token);
    }

    private async Task FetchAsync(string tag, CancellationToken token)
    {
        var result = await radio.FetchStationsAsync(tag, token).ConfigureAwait(false);
        if (token.IsCancellationRequested)
        {
            return;
        }

        stations = result;
        loading = false;
    }

    private void GoToBrowse()
    {
        fetch?.Cancel();
        loading = false;
        view = View.Browse;
    }

    private void GoToReturnView() => view = returnView;

    private bool IsCurrent(RadioStation station)
    {
        return player.State != RadioPlaybackState.Stopped && player.CurrentStation == station.Name;
    }

    private string CategoryTitle()
    {
        return categoryIndex >= 0 ? RadioService.Categories[categoryIndex].Display : DisplayName;
    }

    private string NowPlayingSubtitle(RadioPlaybackState state)
    {
        if (state is RadioPlaybackState.Buffering or RadioPlaybackState.Failed)
        {
            return StateLabel(state);
        }

        return categoryIndex >= 0 ? $"{RadioService.Categories[categoryIndex].Display} · LIVE" : "LIVE";
    }

    private static string Subtitle(RadioStation station)
    {
        var bitrate = station.Bitrate > 0 ? $"{station.Bitrate}kbps" : "live";
        return string.IsNullOrEmpty(station.Country) ? bitrate : $"{bitrate} · {station.Country}";
    }

    private static string StateLabel(RadioPlaybackState state)
    {
        return state switch
        {
            RadioPlaybackState.Buffering => "Buffering…",
            RadioPlaybackState.Playing => "Playing",
            RadioPlaybackState.Failed => "Connection lost",
            _ => string.Empty,
        };
    }

    private static string Truncate(string value, int max)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= max)
        {
            return value ?? string.Empty;
        }

        return value.Substring(0, max - 1) + "…";
    }

    public void Dispose()
    {
        fetch?.Cancel();
        fetch?.Dispose();
        artwork.Dispose();
    }
}
