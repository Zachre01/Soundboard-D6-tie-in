using NAudio.Wave;

namespace DeckSoundboard;

public readonly record struct PlaybackStart(long SessionId, TimeSpan Duration);

public sealed class AudioEngine : IDisposable
{
    private WaveOutEvent? _output;
    private AudioFileReader? _reader;
    private long _nextSessionId;

    public string? CurrentSoundId { get; private set; }
    public long CurrentSessionId { get; private set; }
    public bool IsPlaying => _output?.PlaybackState == PlaybackState.Playing;

    // Kept as a safety/fallback signal. PTT timing is controlled primarily by
    // the exact media duration returned by Play().
    public event Action<long>? PlaybackEnded;

    public IReadOnlyList<(int Number, string Name)> GetOutputDevices()
    {
        var list = new List<(int, string)> { (-1, "Default Windows audio device") };
        for (int i = 0; i < WaveOut.DeviceCount; i++)
        {
            var caps = WaveOut.GetCapabilities(i);
            list.Add((i, caps.ProductName));
        }
        return list;
    }

    public PlaybackStart Play(SoundClipConfig clip, int deviceNumber, float masterVolume)
    {
        Stop(false);
        if (!File.Exists(clip.FilePath)) throw new FileNotFoundException("Audio file not found", clip.FilePath);

        var sessionId = Interlocked.Increment(ref _nextSessionId);
        var reader = new AudioFileReader(clip.FilePath)
        {
            Volume = Math.Clamp(clip.Volume * masterVolume, 0f, 1f)
        };
        var duration = reader.TotalTime;
        var output = new WaveOutEvent { DeviceNumber = deviceNumber };
        output.Init(reader);

        _reader = reader;
        _output = output;
        CurrentSoundId = clip.Id;
        CurrentSessionId = sessionId;

        output.PlaybackStopped += (_, _) => OnPlaybackStopped(output, reader, sessionId);
        output.Play();
        return new PlaybackStart(sessionId, duration);
    }

    private void OnPlaybackStopped(WaveOutEvent output, AudioFileReader reader, long sessionId)
    {
        try { output.Dispose(); } catch { }
        try { reader.Dispose(); } catch { }

        if (CurrentSessionId == sessionId)
        {
            if (ReferenceEquals(_output, output)) _output = null;
            if (ReferenceEquals(_reader, reader)) _reader = null;
            CurrentSoundId = null;
            CurrentSessionId = 0;
        }

        PlaybackEnded?.Invoke(sessionId);
    }

    public void Stop(bool notify = true)
    {
        var output = _output;
        var reader = _reader;
        var sessionId = CurrentSessionId;
        if (output is null) return;

        _output = null;
        _reader = null;
        CurrentSoundId = null;
        CurrentSessionId = 0;

        try { output.Stop(); } catch { }
        try { output.Dispose(); } catch { }
        try { reader?.Dispose(); } catch { }

        if (notify && sessionId != 0)
            PlaybackEnded?.Invoke(sessionId);
    }

    public void Dispose() => Stop(false);
}
