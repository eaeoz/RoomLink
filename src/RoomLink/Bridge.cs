using System;
using System.Threading;
using System.Threading.Tasks;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace RoomLink;

/// <summary>
/// One half of the bridge: continuously captures from a microphone endpoint
/// and plays into a speaker endpoint, using a ring buffer in between so that
/// capture and playback run independently on their own WASAPI clocks.
/// </summary>
public sealed class Bridge : IDisposable
{
    private readonly string _captureId;
    private readonly string _renderId;
    private readonly string _label;

    private WaveInEvent? _capture;
    private WaveOutEvent? _playback;
    private BufferedWaveProvider? _buffer;
    private VolumeSampleProvider? _volumeProvider;
    private float _gain = 1.0f;
    private DateTime _lastLevelLog = DateTime.MinValue;
    private int _levelPercent;

    public string Label => _label;
    public bool IsRunning { get; private set; }

    /// <summary>
    /// Live capture level of this bridge's microphone, 0..100 (rough RMS-based).
    /// Updated continuously while running; read for the UI level meter.
    /// </summary>
    public int LevelPercent => _levelPercent;

    /// <summary>Buffered audio not yet played, in milliseconds. For diagnostics.</summary>
    public int BufferedMilliseconds =>
        _buffer == null ? 0 : (int)(_buffer.BufferedDuration.TotalMilliseconds);

    /// <param name="label">Human-readable name for logs/UI, e.g. "Headset mic → Speakers".</param>
    public Bridge(string label, string captureDeviceId, string renderDeviceId)
    {
        _label = label;
        _captureId = captureDeviceId;
        _renderId = renderDeviceId;
    }

    /// <summary>Per-direction volume, 0..1 (simple gain applied in the pipeline).</summary>
    public float Gain
    {
        get => _gain;
        set => SetGain(value);
    }

    /// <summary>Live volume change, works while the bridge is running.</summary>
    public void SetGain(float value)
    {
        _gain = Math.Clamp(value, 0f, 1f);
        if (_volumeProvider != null)
            _volumeProvider.Volume = _gain;
    }

    public void Start()
    {
        if (IsRunning) return;

        using var enumerator = new MMDeviceEnumerator();
        var captureEndpoint = enumerator.GetDevice(_captureId);
        var renderEndpoint = enumerator.GetDevice(_renderId);

        // ---- Capture side ------------------------------------------------
        // WaveInEvent with device number resolved from the WASAPI endpoint id.
        _capture = new WaveInEvent
        {
            DeviceNumber = ResolveCaptureIndex(captureEndpoint),
            WaveFormat = new WaveFormat(48000, 16, 2),   // fixed format, resample-free path for most BT HFP
            BufferMilliseconds = 40,
        };

        _buffer = new BufferedWaveProvider(_capture.WaveFormat)
        {
            // Drop oldest audio if we fall behind: for an intercom, latency is
            // worse than a glitch. ReadFully = true pads silence when empty so
            // the playback device never sees a short/zero read.
            BufferDuration = TimeSpan.FromMilliseconds(300),
            DiscardOnBufferOverflow = true,
            ReadFully = true,
        };

        _capture.DataAvailable += OnCaptureDataAvailable;

        // ---- Playback side ------------------------------------------------
        // WaveOutEvent: legacy waveOut API. Handles 48k/16-bit stereo on every
        // endpoint via the Windows compat layer — no WASAPI format negotiation
        // to get wrong. (WasapiOut shared-mode conversion stalled in testing.)
        var playback = _playback = new WaveOutEvent
        {
            DeviceNumber = ResolveRenderIndex(renderEndpoint),
            DesiredLatency = 60,
            NumberOfBuffers = 3,
        };
        var volume = _volumeProvider = new VolumeSampleProvider(_buffer.ToSampleProvider())
        {
            Volume = _gain,
        };
        playback.Init(new SampleToWaveProvider16(volume));

        playback.PlaybackStopped += (_, e) =>
        {
            if (IsRunning && e.Exception != null)
            {
                Logger.Write($"[{_label}] playback stopped unexpectedly: {e.Exception}");
                Console.WriteLine($"[{_label}] playback stopped unexpectedly: {e.Exception.Message}");
            }
        };

        // Start playback first so the sink exists when audio starts arriving,
        // then open the capture stream (this triggers the BT HFP profile switch).
        playback.Play();
        _capture.StartRecording();

        IsRunning = true;
        Logger.Write($"[{_label}] STARTED: '{captureEndpoint.FriendlyName}' → '{renderEndpoint.FriendlyName}' " +
                     $"(waveIn #{ResolveCaptureIndex(captureEndpoint)} → waveOut #{playback.DeviceNumber}, " +
                     $"fmt {_capture.WaveFormat})");
        Console.WriteLine($"[{_label}] started ({captureEndpoint.FriendlyName} → {renderEndpoint.FriendlyName})");
    }

    private void OnCaptureDataAvailable(object? sender, WaveInEventArgs e)
    {
        try
        {
            _buffer?.AddSamples(e.Buffer, 0, e.BytesRecorded);
        }
        catch (InvalidOperationException)
        {
            /* buffer race on stop */
        }

        // --- RMS level meter over 16-bit PCM samples ---
        int sampleCount = e.BytesRecorded / 2;
        if (sampleCount > 0)
        {
            double sumSquares = 0;
            for (int i = 0; i < sampleCount; i++)
            {
                short s = BitConverter.ToInt16(e.Buffer, i * 2);
                sumSquares += (double)s * s;
            }
            var rms = Math.Sqrt(sumSquares / sampleCount);
            _levelPercent = Math.Min(100, (int)(rms / 327.67 * 100));
        }

        // Periodic diagnostic line: level + buffer latency.
        if ((DateTime.Now - _lastLevelLog).TotalSeconds >= 2)
        {
            _lastLevelLog = DateTime.Now;
            Logger.Write($"[{_label}] mic level {_levelPercent}%, buffer {BufferedMilliseconds} ms");
        }
    }

    public void Stop()
    {
        if (!IsRunning) return;
        IsRunning = false;

        try { _capture?.StopRecording(); } catch { }
        try { _playback?.Stop(); } catch { }

        // Give WASAPI a moment to unwind callbacks before disposal.
        Thread.Sleep(60);

        _capture?.Dispose(); _capture = null;
        _playback?.Dispose(); _playback = null;
        _buffer = null;
        _levelPercent = 0;

        Logger.Write($"[{_label}] stopped");
        Console.WriteLine($"[{_label}] stopped");
    }

    public void Dispose() => Stop();

    /// <summary>
    /// WaveInEvent uses the legacy waveIn API index, not a WASAPI id.
    /// Find the matching index by friendly name (names are unique per endpoint).
    /// </summary>
    private static int ResolveCaptureIndex(MMDevice endpoint)
    {
        for (int i = 0; i < WaveInEvent.DeviceCount; i++)
        {
            var caps = WaveInEvent.GetCapabilities(i);
            // waveIn caps.ProductName is truncated to 31 chars, so compare prefix.
            var prefix = endpoint.FriendlyName.Length <= 31
                ? endpoint.FriendlyName
                : endpoint.FriendlyName[..31];
            if (string.Equals(caps.ProductName.Trim(), prefix.Trim(), StringComparison.OrdinalIgnoreCase))
                return i;
        }
        throw new InvalidOperationException(
            $"Capture endpoint '{endpoint.FriendlyName}' not found in waveIn list.");
    }

    /// <summary>Same resolution for the playback side (waveOut index).</summary>
    private static int ResolveRenderIndex(MMDevice endpoint)
    {
        for (int i = 0; i < WaveOut.DeviceCount; i++)
        {
            var caps = WaveOut.GetCapabilities(i);
            var prefix = endpoint.FriendlyName.Length <= 31
                ? endpoint.FriendlyName
                : endpoint.FriendlyName[..31];
            if (string.Equals(caps.ProductName.Trim(), prefix.Trim(), StringComparison.OrdinalIgnoreCase))
                return i;
        }
        throw new InvalidOperationException(
            $"Render endpoint '{endpoint.FriendlyName}' not found in waveOut list.");
    }

    /// <summary>
    /// Convenience: run one bridge for a while (used by the console test harness).
    /// </summary>
    public static async Task RunForTestAsync(Bridge bridge, TimeSpan duration)
    {
        bridge.Start();
        await Task.Delay(duration);
        bridge.Stop();
    }
}
