using System.Collections.Concurrent;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Aetherphone.Core.Net;
using Dalamud.Interface.Textures.TextureWraps;

namespace Aetherphone.Core.Media;

internal sealed class RemoteImageCache : IDisposable
{
    private readonly HttpService http;
    private readonly ConcurrentDictionary<string, IDalamudTextureWrap> ready = new();
    private readonly ConcurrentDictionary<string, byte> loading = new();
    private readonly ConcurrentDictionary<string, byte> failed = new();
    private readonly CancellationTokenSource cancellation = new();

    public RemoteImageCache(HttpService http)
    {
        this.http = http;
    }

    public IDalamudTextureWrap? Get(string? url)
    {
        if (string.IsNullOrEmpty(url))
        {
            return null;
        }

        if (ready.TryGetValue(url, out var wrap))
        {
            return wrap;
        }

        if (failed.ContainsKey(url) || !loading.TryAdd(url, 0))
        {
            return null;
        }

        _ = LoadAsync(url);
        return null;
    }

    public Vector2 SizeOf(string? url)
    {
        return url is not null && ready.TryGetValue(url, out var wrap) ? wrap.Size : Vector2.Zero;
    }

    public bool Failed(string? url) => url is not null && failed.ContainsKey(url);

    private async Task LoadAsync(string url)
    {
        try
        {
            var token = cancellation.Token;
            var bytes = await http.GetBytesAsync(new Uri(url), token).ConfigureAwait(false);
            if (bytes is null)
            {
                failed.TryAdd(url, 0);
                return;
            }

            var wrap = await Plugin.TextureProvider.CreateFromImageAsync(bytes, $"Aetherphone.Gram.{url}", token).ConfigureAwait(false);
            if (!ready.TryAdd(url, wrap))
            {
                wrap.Dispose();
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            failed.TryAdd(url, 0);
            AepLog.Warning($"[Aethergram] failed to load image {url}: {exception.Message}");
        }
        finally
        {
            loading.TryRemove(url, out _);
        }
    }

    public void Dispose()
    {
        cancellation.Cancel();
        foreach (var wrap in ready.Values)
        {
            wrap.Dispose();
        }

        ready.Clear();
        cancellation.Dispose();
    }
}
