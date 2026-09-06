namespace DeckSoundboard;

public sealed class SoundboardController : IDisposable
{
    private readonly AppConfig _config;
    private readonly AudioEngine _audio;
    private readonly PttController _ptt;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private CancellationTokenSource? _durationCts;
    private long _activeSessionId;
    private string? _activeSoundId;
    public event Action? StateChanged;

    public SoundboardController(AppConfig config, AudioEngine audio, PttController ptt)
    {
        _config = config; _audio = audio; _ptt = ptt;
        _audio.PlaybackEnded += sessionId => _ = HandlePlaybackEndedSafetyAsync(sessionId);
    }

    public string? CurrentSoundId => _activeSoundId;
    public bool PttHeld => _ptt.IsHeld;

    public async Task ToggleAsync(string soundId)
    {
        await _gate.WaitAsync();
        try
        {
            if (_activeSoundId == soundId)
            {
                CancelDurationTimer();
                _activeSessionId = 0;
                _activeSoundId = null;
                _audio.Stop(false);
                ReleasePtt();
                StateChanged?.Invoke();
                return;
            }

            var clip = _config.Sounds.FirstOrDefault(s => s.Id == soundId);
            if (clip is null) return;

            CancelDurationTimer();
            _activeSessionId = 0;
            _activeSoundId = null;
            if (_audio.IsPlaying || _audio.CurrentSoundId is not null)
                _audio.Stop(false);

            // PTT can be globally disabled for open-mic use. Audio playback is
            // otherwise identical.
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
                // Never leave a previously-held PTT key down if the option was
                // turned off while the program was running.
                _ptt.Release();
            }

            var start = _audio.Play(clip, _config.OutputDeviceNumber, _config.MasterVolume);
            _activeSessionId = start.SessionId;
            _activeSoundId = clip.Id;
            StateChanged?.Invoke();

            // Duration is authoritative: keep PTT held for exactly the media
            // duration, then the optional tail delay. PlaybackStopped remains
            // only a safety fallback.
            if (_config.PttEnabled)
                StartDurationRelease(start.SessionId, start.Duration, _config.PostDelayMs);
        }
        finally { _gate.Release(); }
    }

    private void StartDurationRelease(long sessionId, TimeSpan duration, int tailMs)
    {
        CancelDurationTimer();
        var cts = new CancellationTokenSource();
        _durationCts = cts;
        _ = Task.Run(async () =>
        {
            try
            {
                var total = duration + TimeSpan.FromMilliseconds(Math.Max(0, tailMs));
                if (total > TimeSpan.Zero)
                    await Task.Delay(total, cts.Token);

                await _gate.WaitAsync(cts.Token);
                try
                {
                    if (!cts.IsCancellationRequested && sessionId != 0 && sessionId == _activeSessionId)
                    {
                        _activeSessionId = 0;
                        _activeSoundId = null;
                        ReleasePtt();
                        StateChanged?.Invoke();
                    }
                }
                finally { _gate.Release(); }
            }
            catch (OperationCanceledException) { }
        });
    }

    private async Task HandlePlaybackEndedSafetyAsync(long sessionId)
    {
        // Do NOT release PTT just because NAudio raised PlaybackStopped. Some
        // devices/codecs can report that event early. The duration timer above
        // is authoritative. We only clear playback state after the timer has
        // already completed or after an explicit stop.
        await Task.CompletedTask;
    }

    private void CancelDurationTimer()
    {
        try { _durationCts?.Cancel(); } catch { }
        _durationCts?.Dispose();
        _durationCts = null;
    }

    private void ReleasePtt() => _ptt.Release();

    public async Task EmergencyStopAsync()
    {
        await _gate.WaitAsync();
        try
        {
            CancelDurationTimer();
            _activeSessionId = 0;
            _activeSoundId = null;
            _audio.Stop(false);
            ReleasePtt();
            StateChanged?.Invoke();
        }
        finally { _gate.Release(); }
    }

    public void Dispose()
    {
        CancelDurationTimer();
        _activeSessionId = 0;
        _activeSoundId = null;
        _audio.Stop(false);
        ReleasePtt();
        _gate.Dispose();
    }
}
