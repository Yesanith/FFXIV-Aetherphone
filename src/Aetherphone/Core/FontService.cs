using Dalamud.Interface;
using Dalamud.Interface.ManagedFontAtlas;
using Dalamud.Plugin;

namespace Aetherphone.Core;

internal sealed class FontService : IDisposable
{
    private static readonly float[] Multipliers = { 0.8f, 1.0f, 1.2f, 1.45f, 1.9f };

    private readonly IFontHandle[] handles;

    public FontService(IDalamudPluginInterface pluginInterface)
    {
        var atlas = pluginInterface.UiBuilder.FontAtlas;
        var baseSize = UiBuilder.DefaultFontSizePx;

        handles = new IFontHandle[Multipliers.Length];
        for (var index = 0; index < Multipliers.Length; index++)
        {
            var pixels = baseSize * Multipliers[index];
            handles[index] = atlas.NewDelegateFontHandle(e => e.OnPreBuild(tk => tk.AddDalamudDefaultFont(pixels)));
        }
    }

    public IDisposable Push(float scale) => handles[Nearest(scale)].Push();

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
