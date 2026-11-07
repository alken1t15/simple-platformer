using Microsoft.VisualStudio.TestTools.UnitTesting;
using Platformer.App;
using Platformer.Core;

namespace Platformer.Tests;

[TestClass]
public sealed class AudioCoordinatorTests
{
    internal sealed class Playback : IGameAudioPlayback
    {
        internal sealed class Voice : IGameAudioVoice
        {
            public bool Paused, Disposed;
            public void Pause() => Paused = true;
            public void Resume() => Paused = false;
            public void Dispose() => Disposed = true;
        }
        public List<Voice> Music { get; } = [];
        public List<(GameEventKind Kind, Voice Voice)> Effects { get; } = [];
        public IGameAudioVoice PlayMusic() { var voice = new Voice(); Music.Add(voice); return voice; }
        public IGameAudioVoice PlayEffect(GameEventKind kind)
        { var voice = new Voice(); Effects.Add((kind, voice)); return voice; }
        public void Dispose() { }
    }

    [TestMethod]
    public void DedupPauseTerminalAndNewSessionDoNotReplayOldEventsOrDuplicateMusic()
    {
        var playback = new Playback();
        using var audio = new AudioCoordinator(playback);
        var first = Guid.NewGuid();
        GameEvent coin = new(first, 1, 1, GameEventKind.CoinCollected, "coin");
        audio.Consume(first, [coin]);
        audio.SetScreen(AppScreen.Playing);
        audio.Consume(first, [coin]);
        audio.SetScreen(AppScreen.Playing);
        Assert.HasCount(1, playback.Music);
        Assert.HasCount(1, playback.Effects);
        audio.SetScreen(AppScreen.Paused);
        audio.SetScreen(AppScreen.Settings);
        Assert.IsTrue(playback.Music[0].Paused && playback.Effects[0].Voice.Paused);
        audio.SetScreen(AppScreen.Playing);
        Assert.IsFalse(playback.Music[0].Paused);
        Assert.HasCount(1, playback.Music);
        audio.Stop();
        audio.Consume(first, [coin, new(first, 2, 2, GameEventKind.Failed, null)]);
        audio.SetScreen(AppScreen.Failed);
        Assert.HasCount(2, playback.Effects);
        Assert.IsTrue(playback.Music[0].Disposed);
        var second = Guid.NewGuid();
        audio.Consume(second, [coin, new(second, 1, 1, GameEventKind.Jumped, null)]);
        audio.SetScreen(AppScreen.Playing);
        Assert.HasCount(3, playback.Effects);
        Assert.IsTrue(playback.Effects[1].Voice.Disposed);
        Assert.HasCount(2, playback.Music);
    }

    [TestMethod]
    public void EffectHandlesStayBoundedAndDisposeStopsEverything()
    {
        var playback = new Playback();
        var audio = new AudioCoordinator(playback);
        var id = Guid.NewGuid();
        audio.Consume(id, Enumerable.Range(1, 100).Select(n =>
            new GameEvent(id, n, n, GameEventKind.CoinCollected, n.ToString())).ToArray());
        audio.SetScreen(AppScreen.Playing);
        Assert.AreEqual(16, playback.Effects.Count(item => !item.Voice.Disposed));
        audio.Dispose();
        audio.Dispose();
        Assert.IsTrue(playback.Effects.All(item => item.Voice.Disposed));
        Assert.IsTrue(playback.Music.All(voice => voice.Disposed));
    }

    [TestMethod]
    public void ActualHostDrainsEventsAndPausesAudioOnFocusLoss()
    {
        var playback = new Playback();
        using var audio = new AudioCoordinator(playback);
        var level = SessionFixtures.Level(coins: [new CoinDefinition("coin", new(0, 0.8f, 0), 0.2f)]);
        var host = new GameplayHost(new LevelCatalog([level]), SimulationSettings.Default, new UserSettings(), audio: audio);
        host.Advance(new(true), 0);
        host.Advance(new(true, AppButtons.Enter, AppButtons.Enter), 0);
        host.Advance(new(true), host.Frames.Clock.FixedStepSeconds);
        Assert.AreEqual(GameEventKind.CoinCollected, playback.Effects.Single().Kind);
        Assert.HasCount(1, playback.Music);
        host.Advance(new(false), 0.1);
        Assert.AreEqual(AppScreen.Paused, host.Flow.Screen);
        Assert.IsTrue(playback.Music.Single().Paused);
        host.Advance(new(true), 0.1);
        Assert.IsTrue(playback.Music.Single().Paused);
        host.Advance(new(true, AppButtons.Enter, AppButtons.Enter), 0);
        Assert.IsFalse(playback.Music.Single().Paused);
        Assert.HasCount(1, playback.Effects);
    }
}
