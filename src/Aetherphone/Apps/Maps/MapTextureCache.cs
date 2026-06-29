using Dalamud.Bindings.ImGui;
using Dalamud.Plugin.Services;

namespace Aetherphone.Apps.Maps;

internal sealed class MapTextureCache
{
    private readonly ITextureProvider textures;

    public MapTextureCache(ITextureProvider textures)
    {
        this.textures = textures;
    }

    public bool TryGetHandle(string texturePath, out ImTextureID handle)
    {
        handle = default;
        if (string.IsNullOrEmpty(texturePath))
        {
            return false;
        }

        var wrap = textures.GetFromGame(texturePath).GetWrapOrDefault();
        if (wrap is null)
        {
            return false;
        }

        handle = wrap.Handle;
        return true;
    }
}
