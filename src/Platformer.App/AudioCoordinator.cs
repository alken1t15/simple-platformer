using Platformer.Core;

namespace Platformer.App;

/// <summary>Узкая граница для native playback и CPU-проверок lifecycle.</summary>
public interface IGameAudioVoice : IDisposable
{
    void Pause();
    void Resume();
}

public interface IGameAudioPlayback : IDisposable
{
    IGameAudioVoice PlayMusic(string trackId = MusicTracks.OriginalId);
    IGameAudioVoice PlayEffect(GameEventKind kind);
}

/// <summary>Одна музыка, ограниченные эффекты и дедупликация событий одного сеанса.</summary>
public sealed class AudioCoordinator : IDisposable
{
    private readonly IGameAudioPlayback playback;
    private readonly Queue<IGameAudioVoice> effects = new();
    private IGameAudioVoice? music;
    private Guid sessionId;
    private long sequence;
    private bool paused;
    private bool disposed;
    private string musicTrackId = MusicTracks.OriginalId;

    public AudioCoordinator(IGameAudioPlayback playback) => this.playback = playback ?? throw new ArgumentNullException(nameof(playback));

    /// <summary>Меняет только фон; в Pause новый voice создаётся лишь после Continue.</summary>
    public void SelectMusic(string trackId)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        MusicTracks.Get(trackId);
        if (musicTrackId == trackId) return;
        if (music is not null)
        {
            music.Dispose();
            music = null;
            if (!paused) music = playback.PlayMusic(trackId);
        }
        musicTrackId = trackId;
    }

    public void Consume(Guid currentSession, IReadOnlyList<GameEvent> events)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentNullException.ThrowIfNull(events);
        if (currentSession == Guid.Empty) throw new ArgumentException("Пустой SessionId", nameof(currentSession));
        if (currentSession != sessionId)
        {
            Stop();
            sessionId = currentSession;
            sequence = 0;
        }
        foreach (var item in events)
        {
            if (item.SessionId != sessionId || item.Sequence <= sequence) continue;
            if (item.Kind is GameEventKind.Jumped or GameEventKind.CoinCollected
                or GameEventKind.Failed or GameEventKind.LevelCompleted)
            {
                // Вместе с music не больше 17 sources; не удерживаем законченные handles бесконечно.
                if (effects.Count == 16) effects.Dequeue().Dispose();
                var voice = playback.PlayEffect(item.Kind);
                effects.Enqueue(voice);
                if (paused) voice.Pause();
            }
            sequence = item.Sequence;
        }
    }

    public void SetScreen(AppScreen screen)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (screen == AppScreen.Playing)
        {
            SetPaused(false);
            music ??= playback.PlayMusic(musicTrackId);
        }
        else if (screen is AppScreen.Paused or AppScreen.Settings)
            SetPaused(true);
        else
        {
            music?.Dispose();
            music = null;
        }
    }

    public void SetPaused(bool value)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (paused == value) return;
        paused = value;
        foreach (var voice in effects)
            if (value) voice.Pause(); else voice.Resume();
        if (music is not null)
            if (value) music.Pause(); else music.Resume();
    }

    /// <summary>Переход останавливает voices, но не забывает уже проигранные Sequence.</summary>
    public void Stop()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        music?.Dispose();
        music = null;
        while (effects.TryDequeue(out var voice)) voice.Dispose();
        paused = false;
    }

    public void Dispose()
    {
        if (disposed) return;
        Stop();
        playback.Dispose();
        disposed = true;
    }
}
