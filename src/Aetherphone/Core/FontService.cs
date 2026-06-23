using Dalamud.Interface;
using Dalamud.Interface.ManagedFontAtlas;
using Dalamud.Plugin;

namespace Aetherphone.Core;

internal sealed class FontService : IDisposable
{
    private static readonly float[] Multipliers = { 0.8f, 1.0f, 1.2f, 1.45f, 1.9f };

    private readonly IFontAtlas atlas;
    private readonly float baseSize;

    private IFontHandle[] handles;
    private float zoom;

    public FontService(IDalamudPluginInterface pluginInterface, float zoom)
    {
        atlas = pluginInterface.UiBuilder.FontAtlas;
        baseSize = UiBuilder.DefaultFontSizePx;
        this.zoom = zoom;
        handles = Build(zoom);
    }

    public float Zoom => zoom;

    public void SetZoom(float value)
    {
        if (MathF.Abs(value - zoom) < 0.001f)
        {
            return;
        }

        var previous = handles;
        zoom = value;
        handles = Build(value);

        for (var index = 0; index < previous.Length; index++)
        {
            previous[index].Dispose();
        }
    }

    public IDisposable Push(float scale) => handles[Nearest(scale)].Push();

    private IFontHandle[] Build(float scale)
    {
        var built = new IFontHandle[Multipliers.Length];
        for (var index = 0; index < Multipliers.Length; index++)
        {
            var pixels = baseSize * Multipliers[index] * scale;
            built[index] = atlas.NewDelegateFontHandle(e => e.OnPreBuild(tk => tk.AddDalamudDefaultFont(pixels)));
        }

        return built;
    }

    private static int Nearest(float scale)
    {
        var best = 0;
        var bestDelta = float.MaxValue;
        for (var index = 0; index < Multipliers.Length; index++)
        {
            var delta = MathF.Abs(Multipliers[index] - scale);
            if (delta < bestDelta)
            {
                bestDelta = delta;
                best = index;
            }
        }

        return best;
    }

    public void Dispose()
    {
        for (var index = 0; index < handles.Length; index++)
        {
            handles[index].Dispose();
        }
    }
}
