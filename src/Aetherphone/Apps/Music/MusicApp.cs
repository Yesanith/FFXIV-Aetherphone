using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Net;
using Aetherphone.Core.Playback;
using Aetherphone.Core.Radio;
using Aetherphone.Core.Songs;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin.Services;

namespace Aetherphone.Apps.Music;

internal sealed class MusicApp : IPhoneApp
{
    private enum View : byte
    {
        Browse,
        Stations,
        Search,
        RadioNowPlaying,
        SongNowPlaying,
    }

    private const float MiniHeight = 58f;
    private const float TileHeight = 92f;
    private const float SearchBarHeight = 50f;

    public string Id => "music";

    public string DisplayName => "Music";

    public string Glyph => "M";

    public Vector4 Accent => new(0.96f, 0.36f, 0.52f, 1f);

    public int BadgeCount => 0;

    private readonly RadioService radio;
    private readonly SongSearchService songSearch;
    private readonly PlaybackHub playback;
    private readonly MediaCache media;
    private readonly HttpService http;
    private readonly ArtworkCache artwork;

    private View view = View.Browse;
    private View returnView = View.Browse;
    private int categoryIndex = -1;
    private RadioStation[] stations = Array.Empty<RadioStation>();
    private volatile bool loading;
    private CancellationTokenSource? fetch;

    private Song[] results = Array.Empty<Song>();
    private volatile bool searching;
    private bool hasSearched;
    private CancellationTokenSource? search;
    private string searchDraft = string.Empty;

    private float clock;

    public MusicApp(RadioService radio, SongSearchService songSearch, PlaybackHub playback, MediaCache media, HttpService http, ITextureProvider textures)
    {
        this.radio = radio;
        this.songSearch = songSearch;
        this.playback = playback;
        this.media = media;
        this.http = http;
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

        if (view == View.RadioNowPlaying && !playback.RadioActive)
        {
            view = returnView;
        }

        if (view == View.SongNowPlaying && !playback.SongActive)
        {
            view = returnView;
        }

        switch (view)
        {
            case View.Stations:
                DrawStations(context);
                break;
            case View.Search:
                DrawSearch(context);
                break;
            case View.RadioNowPlaying:
                DrawRadioNowPlaying(context);
                break;
            case View.SongNowPlaying:
                DrawSongNowPlaying(context);
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

        var barRect = SearchBarRect(content, scale);
        if (DrawSearchBar(barRect, theme))
        {
            view = View.Search;
            BeginSearch(searchDraft);
        }

        var body = new Rect(new Vector2(content.Min.X, barRect.Max.Y), new Vector2(content.Max.X, BodyBottom(content, scale)));
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

        var playing = IsCurrentStation(station);
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
        Typography.Draw(new Vector2(textLeft, min.Y + 41f * scale), StationSubtitle(station), Palette.WithAlpha(theme.TextStrong, 0.62f), 0.8f);

        if (playing)
        {
            Equalizer.Draw(dl, new Vector2(max.X - trailing, discCenter.Y), scale, 17f * scale, clock, Accent, 1f, playback.Radio.State == RadioPlaybackState.Playing);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, rowHeight));

        if (hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            playback.PlayStations(stations, index);
        }
    }

    private void DrawSearch(in PhoneContext context)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var theme = context.Theme;
        var content = context.Content;

        AppHeader.Draw(context, "Search", GoToBrowse);

        var barRect = SearchBarRect(content, scale);
        if (DrawSearchBar(barRect, theme))
        {
            BeginSearch(searchDraft);
        }

        var body = new Rect(new Vector2(content.Min.X, barRect.Max.Y), new Vector2(content.Max.X, BodyBottom(content, scale)));

        if (searching)
        {
            Typography.DrawCentered(body.Center, "Searching…", theme.TextMuted);
            DrawMiniPlayer(context, scale);
            return;
        }

        if (results.Length == 0)
        {
            Typography.DrawCentered(body.Center, hasSearched ? "No results" : "Search for a song", theme.TextMuted);
            DrawMiniPlayer(context, scale);
            return;
        }

        using (AppSurface.Begin(body))
        {
            ImGui.Dummy(new Vector2(0f, 2f * scale));
            for (var index = 0; index < results.Length; index++)
            {
                DrawSongRow(theme, scale, results[index], index);
            }
        }

        DrawMiniPlayer(context, scale);
    }

    private void DrawSongRow(PhoneTheme theme, float scale, Song song, int index)
    {
        var rowHeight = 64f * scale;
        var width = ImGui.GetContentRegionAvail().X;
        var origin = ImGui.GetCursorScreenPos();
        var min = origin;
        var max = new Vector2(origin.X + width, origin.Y + rowHeight);
        var dl = ImGui.GetWindowDrawList();

        var playing = IsCurrentSong(song);
        var hovered = ImGui.IsMouseHoveringRect(min, max);
        if (hovered || playing)
        {
            dl.AddRectFilled(min, max, ImGui.GetColorU32(Palette.WithAlpha(playing ? Accent : theme.TextStrong, playing ? 0.10f : 0.06f)), 14f * scale);
        }

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var thumbSize = 46f * scale;
        var thumbMin = new Vector2(min.X + 10f * scale, min.Y + (rowHeight - thumbSize) * 0.5f);
        var thumbMax = thumbMin + new Vector2(thumbSize, thumbSize);
        DrawThumb(dl, thumbMin, thumbMax, song.ThumbnailUrl, song.Title, 10f * scale);

        var trailing = playing ? 26f * scale : 12f * scale;
        var textLeft = thumbMax.X + 12f * scale;
        var nameColor = playing ? Accent : theme.TextStrong;
        Typography.Draw(new Vector2(textLeft, min.Y + 12f * scale), Truncate(song.Title, 26), nameColor, 1.05f);
        Typography.Draw(new Vector2(textLeft, min.Y + 36f * scale), SongRowSubtitle(song), Palette.WithAlpha(theme.TextStrong, 0.62f), 0.8f);

        if (playing)
        {
            Equalizer.Draw(dl, new Vector2(max.X - trailing, min.Y + rowHeight * 0.5f), scale, 16f * scale, clock, Accent, 1f, playback.Songs.State == SongPlaybackState.Playing);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, rowHeight));

        if (hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            PlaySong(results, index);
        }
    }

    private void DrawRadioNowPlaying(in PhoneContext context)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var theme = context.Theme;
        var content = context.Content;
        var station = playback.Radio.CurrentStation;
        var state = playback.Radio.State;
        var swatch = ArtGradient.FromName(station);
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
        dl.AddImageRounded(artwork.HandleForName(station), artMin, artMax, Vector2.Zero, Vector2.One, 0xFFFFFFFFu, artRounding, ImDrawFlags.RoundCornersAll);
        var pulse = state == RadioPlaybackState.Buffering ? 0.5f + 0.5f * MathF.Abs(MathF.Sin(clock * 3f)) : 1f;
        Equalizer.Draw(dl, new Vector2(artMax.X - 22f * scale, artMax.Y - 18f * scale), scale, 16f * scale, clock, new Vector4(1f, 1f, 1f, 1f), pulse, state == RadioPlaybackState.Playing);

        var nameY = artMax.Y + 30f * scale;
        Typography.DrawCentered(new Vector2(centerX, nameY), Truncate(station, 22), theme.TextStrong, 1.45f);
        Typography.DrawCentered(new Vector2(centerX, nameY + 26f * scale), RadioNowPlayingSubtitle(state), Palette.WithAlpha(Accent, 0.95f), 0.9f);

        var controlsY = nameY + 70f * scale;
        if (playback.Radio.HasQueue)
        {
            if (TransportButton.Draw(new Vector2(centerX - 64f * scale, controlsY), 22f * scale, TransportAction.Previous, Accent, theme.TextStrong, 1f, true))
            {
                playback.Previous();
            }

            if (TransportButton.Draw(new Vector2(centerX + 64f * scale, controlsY), 22f * scale, TransportAction.Next, Accent, theme.TextStrong, 1f, true))
            {
                playback.Next();
            }
        }

        if (TransportButton.Draw(new Vector2(centerX, controlsY), 30f * scale, TransportAction.Stop, Accent, theme.TextStrong, 1f, true))
        {
            playback.Stop();
            GoToReturnView();
        }

        var trackY = controlsY + 52f * scale;
        var trackRect = new Rect(new Vector2(body.Min.X + 28f * scale, trackY - 3f * scale), new Vector2(body.Max.X - 28f * scale, trackY + 3f * scale));
        playback.Volume = Scrubber.Draw(trackRect, playback.Volume, Accent, Palette.WithAlpha(theme.TextStrong, 0.18f), 1f);
    }

    private void DrawSongNowPlaying(in PhoneContext context)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var theme = context.Theme;
        var content = context.Content;
        var songs = playback.Songs;
        var swatch = ArtGradient.FromName(songs.CurrentTitle);
        var dl = ImGui.GetWindowDrawList();

        var tintTop = ImGui.GetColorU32(Palette.WithAlpha(swatch.Top, 0.42f));
        var tintBottom = ImGui.GetColorU32(Palette.WithAlpha(swatch.Top, 0f));
        dl.AddRectFilledMultiColor(content.Min, new Vector2(content.Max.X, content.Min.Y + content.Height * 0.6f), tintTop, tintTop, tintBottom, tintBottom);

        AppHeader.Draw(context, "Now Playing", GoToReturnView);

        var body = new Rect(new Vector2(content.Min.X, content.Min.Y + AppHeader.Height * scale), content.Max);
        var centerX = body.Center.X;

        var artSize = MathF.Min(body.Width * 0.58f, 156f * scale);
        var artTop = body.Min.Y + 10f * scale;
        var artMin = new Vector2(centerX - artSize * 0.5f, artTop);
        var artMax = artMin + new Vector2(artSize, artSize);
        var artRounding = 18f * scale;

        dl.AddRectFilled(artMin + new Vector2(0f, 10f * scale), artMax + new Vector2(0f, 12f * scale), ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.35f)), artRounding);
        DrawThumb(dl, artMin, artMax, songs.CurrentThumbnail, songs.CurrentTitle, artRounding);

        var nameY = artMax.Y + 26f * scale;
        Typography.DrawCentered(new Vector2(centerX, nameY), Truncate(songs.CurrentTitle, 26), theme.TextStrong, 1.3f);
        Typography.DrawCentered(new Vector2(centerX, nameY + 23f * scale), Truncate(SongNowPlayingSubtitle(), 30), Palette.WithAlpha(Accent, 0.95f), 0.85f);

        var duration = songs.Duration;
        var position = songs.Position;
        var fraction = duration > 0f ? Math.Clamp(position / duration, 0f, 1f) : 0f;

        var trackY = nameY + 52f * scale;
        var trackRect = new Rect(new Vector2(body.Min.X + 28f * scale, trackY - 3f * scale), new Vector2(body.Max.X - 28f * scale, trackY + 3f * scale));
        var newFraction = Scrubber.Draw(trackRect, fraction, Accent, Palette.WithAlpha(theme.TextStrong, 0.18f), 1f);
        if (duration > 0f && MathF.Abs(newFraction - fraction) > 0.0025f)
        {
            songs.Seek(newFraction * duration);
        }

        Typography.Draw(new Vector2(trackRect.Min.X, trackY + 8f * scale), FormatTime((int)position), theme.TextMuted, 0.72f);
        var durationLabel = FormatTime((int)duration);
        var durationSize = Typography.Measure(durationLabel, 0.72f);
        Typography.Draw(new Vector2(trackRect.Max.X - durationSize.X, trackY + 8f * scale), durationLabel, theme.TextMuted, 0.72f);

        var controlsY = trackY + 42f * scale;
        if (playback.HasQueue)
        {
            if (TransportButton.Draw(new Vector2(centerX - 64f * scale, controlsY), 22f * scale, TransportAction.Previous, Accent, theme.TextStrong, 1f, true))
            {
                playback.Previous();
            }

            if (TransportButton.Draw(new Vector2(centerX + 64f * scale, controlsY), 22f * scale, TransportAction.Next, Accent, theme.TextStrong, 1f, true))
            {
                playback.Next();
            }
        }

        if (TransportButton.Draw(new Vector2(centerX, controlsY), 30f * scale, TransportAction.Stop, Accent, theme.TextStrong, 1f, true))
        {
            playback.Stop();
            GoToReturnView();
        }

        var volumeY = controlsY + 44f * scale;
        var volumeRect = new Rect(new Vector2(body.Min.X + 42f * scale, volumeY - 2.5f * scale), new Vector2(body.Max.X - 42f * scale, volumeY + 2.5f * scale));
        playback.Volume = Scrubber.Draw(volumeRect, playback.Volume, Accent, Palette.WithAlpha(theme.TextStrong, 0.18f), 1f);
    }

    private void DrawMiniPlayer(in PhoneContext context, float scale)
    {
        if (!playback.IsActive)
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
        if (playback.SongActive)
        {
            var artMin = new Vector2(discCenter.X - discRadius, discCenter.Y - discRadius);
            var artMax = new Vector2(discCenter.X + discRadius, discCenter.Y + discRadius);
            DrawThumb(dl, artMin, artMax, playback.Songs.CurrentThumbnail, playback.Title, 9f * scale);
        }
        else
        {
            ArtGradient.DrawDisc(dl, discCenter, discRadius, ArtGradient.FromName(playback.Title), 1f);
        }

        var textLeft = discCenter.X + discRadius + 12f * scale;
        var stopCenter = new Vector2(max.X - 26f * scale, discCenter.Y);
        Typography.Draw(new Vector2(textLeft, min.Y + 11f * scale), Truncate(playback.Title, 18), theme.TextStrong, 0.95f);
        Typography.Draw(new Vector2(textLeft, min.Y + 31f * scale), Truncate(playback.Subtitle, 22), theme.TextMuted, 0.8f);

        var stopped = TransportButton.Draw(stopCenter, 16f * scale, TransportAction.Stop, Accent, theme.TextStrong, 1f, true);
        if (stopped)
        {
            playback.Stop();
            return;
        }

        if (hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left) && !ImGui.IsMouseHoveringRect(stopCenter - new Vector2(16f * scale, 16f * scale), stopCenter + new Vector2(16f * scale, 16f * scale)))
        {
            returnView = view;
            view = playback.SongActive ? View.SongNowPlaying : View.RadioNowPlaying;
        }

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }
    }

    private bool DrawSearchBar(Rect bar, PhoneTheme theme)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var dl = ImGui.GetWindowDrawList();
        var pillMin = new Vector2(bar.Min.X + 2f * scale, bar.Min.Y + 9f * scale);
        var pillMax = new Vector2(bar.Max.X - 2f * scale, bar.Max.Y - 9f * scale);
        dl.AddRectFilled(pillMin, pillMax, ImGui.GetColorU32(theme.GroupedCard), (pillMax.Y - pillMin.Y) * 0.5f);

        ImGui.SetCursorScreenPos(new Vector2(pillMin.X + 16f * scale, (pillMin.Y + pillMax.Y) * 0.5f - ImGui.GetFrameHeight() * 0.5f));
        ImGui.SetNextItemWidth(pillMax.X - pillMin.X - 32f * scale);

        var submitted = false;
        using (ImRaii.PushColor(ImGuiCol.FrameBg, new Vector4(0f, 0f, 0f, 0f)))
        using (ImRaii.PushColor(ImGuiCol.Text, theme.TextStrong))
        {
            if (ImGui.InputTextWithHint("##songSearch", "Search songs", ref searchDraft, 120, ImGuiInputTextFlags.EnterReturnsTrue))
            {
                submitted = true;
            }
        }

        return submitted && !string.IsNullOrWhiteSpace(searchDraft);
    }

    private void DrawThumb(ImDrawListPtr dl, Vector2 min, Vector2 max, string url, string fallbackName, float rounding)
    {
        var result = Thumb(url);
        if (result.Texture is not null)
        {
            dl.AddImageRounded(result.Texture.Handle, min, max, Vector2.Zero, Vector2.One, 0xFFFFFFFFu, rounding, ImDrawFlags.RoundCornersAll);
            return;
        }

        var swatch = ArtGradient.FromName(fallbackName);
        dl.AddRectFilled(min, max, ImGui.GetColorU32(swatch.Bottom), rounding);
        dl.AddRectFilledMultiColor(min, new Vector2(max.X, (min.Y + max.Y) * 0.5f), ImGui.GetColorU32(swatch.Top), ImGui.GetColorU32(swatch.Top), 0u, 0u);
    }

    private MediaResult Thumb(string url)
    {
        if (string.IsNullOrEmpty(url))
        {
            return default;
        }

        return media.GetOrRequest(url, token => http.GetBytesAsync(new Uri(url), token));
    }

    private Rect ScrollBody(Rect content, float scale)
    {
        var top = content.Min.Y + AppHeader.Height * scale;
        return new Rect(new Vector2(content.Min.X, top), new Vector2(content.Max.X, BodyBottom(content, scale)));
    }

    private Rect SearchBarRect(Rect content, float scale)
    {
        var top = content.Min.Y + AppHeader.Height * scale;
        return new Rect(new Vector2(content.Min.X, top), new Vector2(content.Max.X, top + SearchBarHeight * scale));
    }

    private float BodyBottom(Rect content, float scale)
    {
        return content.Max.Y - (playback.IsActive ? MiniHeight * scale + 4f * scale : 0f);
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

    private void BeginSearch(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return;
        }

        search?.Cancel();
        search?.Dispose();
        search = new CancellationTokenSource();
        var token = search.Token;
        searching = true;
        hasSearched = true;
        results = Array.Empty<Song>();
        _ = SearchAsync(query, token);
    }

    private async Task SearchAsync(string query, CancellationToken token)
    {
        var found = await songSearch.SearchAsync(query, token).ConfigureAwait(false);
        if (token.IsCancellationRequested)
        {
            return;
        }

        results = found;
        searching = false;
    }

    private void PlaySong(Song[] list, int index)
    {
        returnView = view;
        playback.PlaySongs(list, index);
        view = View.SongNowPlaying;
    }

    private void GoToBrowse()
    {
        fetch?.Cancel();
        search?.Cancel();
        loading = false;
        searching = false;
        view = View.Browse;
    }

    private void GoToReturnView() => view = returnView;

    private bool IsCurrentStation(RadioStation station)
    {
        return playback.RadioActive && playback.Radio.CurrentStation == station.Name;
    }

    private bool IsCurrentSong(Song song)
    {
        return playback.SongActive && playback.Songs.CurrentVideoId == song.VideoId;
    }

    private string CategoryTitle()
    {
        return categoryIndex >= 0 ? RadioService.Categories[categoryIndex].Display : DisplayName;
    }

    private string RadioNowPlayingSubtitle(RadioPlaybackState state)
    {
        if (state is RadioPlaybackState.Buffering or RadioPlaybackState.Failed)
        {
            return RadioStateLabel(state);
        }

        return categoryIndex >= 0 ? $"{RadioService.Categories[categoryIndex].Display} · LIVE" : "LIVE";
    }

    private string SongNowPlayingSubtitle()
    {
        var songs = playback.Songs;
        return songs.State switch
        {
            SongPlaybackState.Resolving => "Loading…",
            SongPlaybackState.Buffering => "Buffering…",
            SongPlaybackState.Failed => "Couldn't play this track",
            _ => songs.CurrentAuthor,
        };
    }

    private static string SongRowSubtitle(Song song)
    {
        var author = Truncate(song.Author, 20);
        return string.IsNullOrEmpty(author) ? FormatTime(song.DurationSeconds) : $"{author} · {FormatTime(song.DurationSeconds)}";
    }

    private static string StationSubtitle(RadioStation station)
    {
        var bitrate = station.Bitrate > 0 ? $"{station.Bitrate}kbps" : "live";
        return string.IsNullOrEmpty(station.Country) ? bitrate : $"{bitrate} · {station.Country}";
    }

    private static string RadioStateLabel(RadioPlaybackState state)
    {
        return state switch
        {
            RadioPlaybackState.Buffering => "Buffering…",
            RadioPlaybackState.Playing => "Playing",
            RadioPlaybackState.Failed => "Connection lost",
            _ => string.Empty,
        };
    }

    private static string FormatTime(int totalSeconds)
    {
        if (totalSeconds < 0)
        {
            totalSeconds = 0;
        }

        var minutes = totalSeconds / 60;
        var seconds = totalSeconds % 60;
        return $"{minutes}:{seconds:D2}";
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
        search?.Cancel();
        search?.Dispose();
        artwork.Dispose();
    }
}
