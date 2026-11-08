using Engine3D.Audio;
using Platformer.Core;

namespace Platformer.App;

/// <summary>Загрузка из output, без зависимости от cwd; устройство принадлежит Program.</summary>
public sealed class NativeAudioPlayback : IGameAudioPlayback
{
    private readonly AudioDevice device;
    private readonly Dictionary<GameEventKind, AudioClip> effects = new();
    private readonly Dictionary<string, AudioClip> music = new(StringComparer.Ordinal);
    private bool disposed;

    public NativeAudioPlayback(AudioDevice device, string directory)
    {
        this.device = device ?? throw new ArgumentNullException(nameof(device));
        try
        {
            foreach (var track in MusicTracks.All)
                music.Add(track.Id, device.LoadWave(Path.Combine(directory, track.FileName)));
            foreach (var (kind, name) in new[] {
                (GameEventKind.Jumped, "jump.wav"), (GameEventKind.CoinCollected, "coin.wav"),
                (GameEventKind.Failed, "fail.wav"), (GameEventKind.LevelCompleted, "finish.wav") })
                effects.Add(kind, device.LoadWave(Path.Combine(directory, name)));
        }
        catch { Dispose(); throw; }
    }

    public IGameAudioVoice PlayMusic(string trackId = MusicTracks.OriginalId)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        MusicTracks.Get(trackId);
        return new Voice(device.Play(music[trackId], loop: true, volume: 0.25f));
    }

    public IGameAudioVoice PlayEffect(GameEventKind kind)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        return new Voice(device.Play(effects[kind], volume: 0.7f));
    }

    public void Dispose()
    {
        if (disposed) return;
        foreach (var clip in effects.Values) clip.Dispose();
        foreach (var clip in music.Values) clip.Dispose();
        disposed = true;
    }

    private sealed class Voice(AudioVoice voice) : IGameAudioVoice
    {
        public void Pause() => voice.Pause();
        public void Resume() => voice.Resume();
        public void Dispose() => voice.Dispose();
    }
}
