namespace DeckSoundboard;

public sealed class SoundboardController : IDisposable
{
    private readonly AppConfig _config;
    private readonly AudioEngine _audio;
    private readonly PttController _ptt;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private CancellationTokenSource? _tailReleaseCts;
    private long _activeSessionId;
    private string? _activeSoundId;
    private bool _disposed;

    public event Action? StateChanged;

    public SoundboardController(AppConfig config, AudioEngine audio, PttController ptt)
    {
        _config = config;
        _audio = audio;
        _ptt = ptt;
        _audio.PlaybackEnded += sessionId => _ = HandlePlaybackEndedAsync(sessionId);
    }

    public string? CurrentSoundId => _activeSoundId;
    public bool PttHeld => _ptt.IsHeld;

    public async Task ToggleAsync(string soundId)
    {
        await _gate.WaitAsync();
        try
        {
            if (_disposed) return;

            // Pressing the same D6 key while its clip is actively playing is
            // an immediate stop + PTT release.
            if (_activeSoundId == soundId && _activeSessionId != 0)
            {
                CancelTailRelease();
                _activeSessionId = 0;
                _activeSoundId = null;
                _audio.Stop(false);
                ReleasePtt();
                StateChanged?.Invoke();
                return;
            }

            var clip = _config.Sounds.FirstOrDefault(s => s.Id == soundId);
            if (clip is null) return;

            // If the previous clip has finished but we are still inside its
            // configured PTT tail delay, reuse the already-held PTT for the
            // new clip instead of releasing and immediately pressing again.
            CancelTailRelease();

            // Invalidate the old playback session BEFORE stopping it. Any
            // delayed PlaybackStopped event from that session is therefore
            // guaranteed to be ignored by HandlePlaybackEndedAsync().
            _activeSessionId = 0;
            _activeSoundId = null;
            if (_audio.IsPlaying || _audio.CurrentSoundId is not null)
                _audio.Stop(false);

            if (_config.PttEnabled)
            {
                if (!_ptt.IsHeld && !_ptt.Press(_config.PttKey))
                    throw new InvalidOperationException($"Unknown PTT key '{_config.PttKey}'.");
                StateChanged?.Invoke();

                if (_config.PreDelayMs > 0)
                    await Task.Delay(_config.PreDelayMs);
            }
            else
            {
                // If PTT was disabled while a prior sound was active, ensure
                // no synthetic key remains held.
                _ptt.Release();
            }

            var start = _audio.Play(clip, _config.OutputDeviceNumber, _config.MasterVolume);
            _activeSessionId = start.SessionId;
            _activeSoundId = clip.Id;
            StateChanged?.Invoke();

            // IMPORTANT: there is intentionally NO duration timer here.
            // PTT remains held until the active WaveOut playback session
            // reports that it has actually stopped. This avoids early release
            // caused by inaccurate MP3/VBR duration metadata.
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task HandlePlaybackEndedAsync(long sessionId)
    {
        await _gate.WaitAsync();
        try
        {
            if (_disposed) return;

            // Ignore stale stop events from a clip that was replaced/stopped.
            if (sessionId == 0 || sessionId != _activeSessionId)
                return;

            // Natural end of the CURRENT clip. Playback itself is finished,
            // so clear the sound state now, but keep PTT down for the optional
            // tail delay to cover audio-device/Wave Link buffering.
            _activeSoundId = null;
            StateChanged?.Invoke();

            if (!_config.PttEnabled)
            {
                _activeSessionId = 0;
                ReleasePtt();
                StateChanged?.Invoke();
                return;
            }

            StartTailRelease(sessionId, _config.PostDelayMs);
        }
        finally
        {
            _gate.Release();
        }
    }

    private void StartTailRelease(long sessionId, int tailMs)
    {
        CancelTailRelease();
        var cts = new CancellationTokenSource();
        _tailReleaseCts = cts;

        _ = Task.Run(async () =>
        {
            try
            {
                var delay = Math.Max(0, tailMs);
                if (delay > 0)
                    await Task.Delay(delay, cts.Token);

                await _gate.WaitAsync(cts.Token);
                try
                {
                    if (_disposed || cts.IsCancellationRequested)
                        return;

                    // A new clip may have started during the tail delay. Only
                    // the session that actually ended may release its PTT.
                    if (sessionId != 0 && sessionId == _activeSessionId && _activeSoundId is null)
                    {
                        _activeSessionId = 0;
                        ReleasePtt();
                        StateChanged?.Invoke();
                    }
                }
                finally
                {
                    _gate.Release();
                }
            }
            catch (OperationCanceledException) { }
        });
    }

    private void CancelTailRelease()
    {
        try { _tailReleaseCts?.Cancel(); } catch { }
        _tailReleaseCts?.Dispose();
        _tailReleaseCts = null;
    }

    private void ReleasePtt() => _ptt.Release();

    public async Task EmergencyStopAsync()
    {
        await _gate.WaitAsync();
        try
        {
            CancelTailRelease();
            _activeSessionId = 0;
            _activeSoundId = null;
            _audio.Stop(false);
            ReleasePtt();
            StateChanged?.Invoke();
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        CancelTailRelease();
        _activeSessionId = 0;
        _activeSoundId = null;
        _audio.Stop(false);
        ReleasePtt();
        _gate.Dispose();
    }
}
