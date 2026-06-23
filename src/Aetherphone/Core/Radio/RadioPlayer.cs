using System.IO;
using System.Net.Http;
using System.Threading;
using NAudio.Wave;

namespace Aetherphone.Core.Radio;

internal enum RadioPlaybackState : byte
{
    Stopped,
    Buffering,
    Playing,
    Failed,
}

internal sealed class RadioPlayer : IDisposable
{
    private static readonly TimeSpan BufferDuration = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan PrebufferThreshold = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan BackpressureThreshold = TimeSpan.FromSeconds(15);

    private readonly HttpClient client;
    private readonly object gate = new();

    private CancellationTokenSource? cancellation;
    private Thread? worker;
    private volatile RadioPlaybackState state = RadioPlaybackState.Stopped;
    private volatile string currentStation = string.Empty;
    private float volume = 0.6f;

    private RadioStation[] queue = Array.Empty<RadioStation>();
    private int queueIndex = -1;

    public RadioPlayer()
    {
        client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd($"Aetherphone/{AepConstants.Version} (+https://github.com/XeldarAlz/FFXIV-Aetherphone)");
    }

    public RadioPlaybackState State => state;

    public string CurrentStation => currentStation;

    public bool HasQueue => queue.Length > 1;

    public float Volume
    {
        get => volume;
        set => volume = Math.Clamp(value, 0f, 1f);
    }

    public void Play(RadioStation[] stations, int index)
    {
        if (stations is null || stations.Length == 0)
        {
            return;
        }

        var start = Math.Clamp(index, 0, stations.Length - 1);
        lock (gate)
        {
            queue = stations;
            queueIndex = start;
        }

        StartStation(stations[start]);
    }

    public void Next() => Skip(1);

    public void Previous() => Skip(-1);

    private void Skip(int direction)
    {
        RadioStation station;
        lock (gate)
        {
            if (queue.Length == 0)
            {
                return;
            }

            queueIndex = ((queueIndex + direction) % queue.Length + queue.Length) % queue.Length;
            station = queue[queueIndex];
        }

        StartStation(station);
    }

    private void StartStation(RadioStation station)
    {
        Stop();

        lock (gate)
        {
            currentStation = station.Name;
            state = RadioPlaybackState.Buffering;
            cancellation = new CancellationTokenSource();
            var token = cancellation.Token;
            var url = station.StreamUrl;
            worker = new Thread(() => Stream(url, token))
            {
                IsBackground = true,
                Name = "Aetherphone.Radio",
            };
            worker.Start();
        }
    }

    public void Stop()
    {
        Thread? toJoin;
        CancellationTokenSource? toDispose;
        lock (gate)
        {
            toJoin = worker;
            toDispose = cancellation;
            worker = null;
            cancellation = null;
        }

        toDispose?.Cancel();
        if (toJoin is not null && toJoin.IsAlive && toJoin != Thread.CurrentThread)
        {
            toJoin.Join(TimeSpan.FromSeconds(3));
        }

        toDispose?.Dispose();
        state = RadioPlaybackState.Stopped;
        currentStation = string.Empty;
    }

    private void Stream(string url, CancellationToken token)
    {
        IWavePlayer? output = null;
        IMp3FrameDecompressor? decompressor = null;
        BufferedWaveProvider? buffer = null;
        var decoded = new byte[16384 * 4];

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            using var response = client.Send(request, HttpCompletionOption.ResponseHeadersRead, token);
            response.EnsureSuccessStatusCode();

            using var network = response.Content.ReadAsStream(token);
            var source = new ReadFullyStream(network);

            while (!token.IsCancellationRequested)
            {
                if (buffer is not null && buffer.BufferedDuration > BackpressureThreshold)
                {
                    Thread.Sleep(200);
                    if (output is not null)
                    {
                        output.Volume = volume;
                    }

                    continue;
                }

                Mp3Frame? frame;
                try
                {
                    frame = Mp3Frame.LoadFromStream(source);
                }
                catch (EndOfStreamException)
                {
                    break;
                }

                if (frame is null)
                {
                    break;
                }

                if (decompressor is null)
                {
                    decompressor = CreateDecompressor(frame);
                    buffer = new BufferedWaveProvider(decompressor.OutputFormat)
                    {
                        BufferDuration = BufferDuration,
                        DiscardOnBufferOverflow = true,
                    };
                }

                var count = decompressor.DecompressFrame(frame, decoded, 0);
                buffer!.AddSamples(decoded, 0, count);

                if (output is null && buffer.BufferedDuration >= PrebufferThreshold)
                {
                    output = new WaveOutEvent { Volume = volume };
                    output.Init(buffer);
                    output.Play();
                    state = RadioPlaybackState.Playing;
                }

                if (output is not null)
                {
                    output.Volume = volume;
                }
            }

            if (!token.IsCancellationRequested && state != RadioPlaybackState.Failed)
            {
                state = RadioPlaybackState.Stopped;
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            state = RadioPlaybackState.Failed;
            AepLog.Warning($"Radio playback failed: {exception.Message}");
        }
        finally
        {
            output?.Stop();
            output?.Dispose();
            decompressor?.Dispose();
        }
    }

    private static IMp3FrameDecompressor CreateDecompressor(Mp3Frame frame)
    {
        var format = new Mp3WaveFormat(frame.SampleRate, frame.ChannelMode == ChannelMode.Mono ? 1 : 2, frame.FrameLength, frame.BitRate);
        return new AcmMp3FrameDecompressor(format);
    }

    public void Dispose()
    {
        Stop();
        client.Dispose();
    }
}

internal sealed class ReadFullyStream : Stream
{
    private readonly Stream source;

    public ReadFullyStream(Stream source)
    {
        this.source = source;
    }

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => 0;
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        var total = 0;
        while (total < count)
        {
            var read = source.Read(buffer, offset + total, count - total);
            if (read == 0)
            {
                break;
            }

            total += read;
        }

        return total;
    }

    public override void Flush() => throw new NotSupportedException();

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
