using Engine3D.Audio;
using Platformer.Core;

namespace Platformer.App;

/// <summary>Загрузка из output, без зависимости от cwd; устройство принадлежит Program.</summary>
public sealed class NativeAudioPlayback : IGameAudioPlayback
{
    private readonly AudioDevice device;
    private readonly Dictionary<GameEventKind, AudioClip> effects = new();
    private AudioClip? music;
    private bool disposed;

    public NativeAudioPlayback(AudioDevice device, string directory)
    {
        this.device = device ?? throw new ArgumentNullException(nameof(device));
        try
        {
            music = device.LoadWave(Path.Combine(directory, "music.wav"));
            foreach (var (kind, name) in new[] {
                (GameEventKind.Jumped, "jump.wav"), (GameEventKind.CoinCollected, "coin.wav"),
                (GameEventKind.Failed, "fail.wav"), (GameEventKind.LevelCompleted, "finish.wav") })
                effects.Add(kind, device.LoadWave(Path.Combine(directory, name)));
        }
        catch { Dispose(); throw; }
    }

    public IGameAudioVoice PlayMusic()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        return new Voice(device.Play(music!, loop: true, volume: 0.25f));
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
        music?.Dispose();
        disposed = true;
    }

    private sealed class Voice(AudioVoice voice) : IGameAudioVoice
    {
        public void Pause() => voice.Pause();
        public void Resume() => voice.Resume();
        public void Dispose() => voice.Dispose();
    }
}
