using System.Numerics;
using System.Security.Cryptography;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Platformer.App;
using Platformer.Core;

namespace Platformer.Tests;

[TestClass]
public sealed class MusicSelectionTests
{
    [TestMethod]
    public void OriginalRemainsDefaultAndInvalidSelectionCannotChangeIt()
    {
        var settings = new UserSettings();
        Assert.AreEqual(MusicTracks.OriginalId, settings.MusicTrackId);
        Assert.ThrowsExactly<ArgumentException>(() => settings.MusicTrackId = "../music");
        Assert.AreEqual(MusicTracks.OriginalId, settings.MusicTrackId);
        Assert.AreEqual(MusicTracks.ExplorationId, MusicTracks.Cycle(MusicTracks.OriginalId, -1));
    }

    [TestMethod]
    public void SelectingDuringGameplayReplacesOnlyMusicAndDoesNotRestartOnEveryFrame()
    {
        var playback = new AudioCoordinatorTests.Playback();
        using var audio = new AudioCoordinator(playback);
        Guid session = Guid.NewGuid();
        audio.Consume(session, [new(session, 1, 1, GameEventKind.Jumped, null)]);
        audio.SetScreen(AppScreen.Playing);
        var old = playback.Music.Single();
        var effect = playback.Effects.Single().Voice;
        audio.SelectMusic(MusicTracks.ExplorationId);
        Assert.IsTrue(old.Disposed);
        Assert.IsFalse(effect.Disposed);
        Assert.AreEqual(MusicTracks.ExplorationId, playback.MusicIds[^1]);
        for (int frame = 0; frame < 50; frame++)
        { audio.SelectMusic(MusicTracks.ExplorationId); audio.SetScreen(AppScreen.Playing); }
        Assert.HasCount(2, playback.Music);
        audio.Dispose();
        Assert.IsTrue(playback.Music.All(voice => voice.Disposed));
    }

    [TestMethod]
    public void MenuChoiceDoesNotStartMusicAndPausedChoiceWaitsForContinue()
    {
        var playback = new AudioCoordinatorTests.Playback();
        using var audio = new AudioCoordinator(playback);
        audio.SelectMusic(MusicTracks.ExplorationId);
        audio.SetScreen(AppScreen.Settings);
        Assert.IsEmpty(playback.Music);
        audio.SetScreen(AppScreen.Playing);
        Assert.AreEqual(MusicTracks.ExplorationId, playback.MusicIds.Single());
        audio.SetScreen(AppScreen.Paused);
        Assert.IsTrue(playback.Music.Single().Paused);
        audio.SelectMusic(MusicTracks.OriginalId);
        audio.SetScreen(AppScreen.Settings);
        Assert.IsTrue(playback.Music.Single().Disposed);
        Assert.HasCount(1, playback.Music, "Новый трек не должен кратко звучать перед Pause");
        audio.SetScreen(AppScreen.Playing);
        Assert.AreEqual(MusicTracks.OriginalId, playback.MusicIds[^1]);
        Assert.IsFalse(playback.Music[^1].Paused);
    }

    [TestMethod]
    public void HostMenuClickAndArrowsSelectMusicWithoutResettingTheAttempt()
    {
        var playback = new AudioCoordinatorTests.Playback();
        using var audio = new AudioCoordinator(playback);
        var host = new GameplayHost(new([SessionFixtures.Level(finishX: 100)]), SimulationSettings.Default, new(), audio: audio);
        void Click(string id)
        {
            host.Advance(new(true), 0);
            var b = host.View.Controls.Single(control => control.Id == id).Bounds;
            Vector2 point = new(b.X + b.Width / 2, b.Y + b.Height / 2);
            host.Advance(new(true, AppButtons.PointerLeft, AppButtons.PointerLeft, default, point), 0);
            host.Advance(new(true, PointerPosition: point), 0);
        }
        Click("settings"); Click("music");
        Assert.AreEqual(MusicTracks.ExplorationId, host.Flow.UserSettings.MusicTrackId);
        Assert.IsEmpty(playback.Music);
        Click("back"); Click("play");
        Assert.AreEqual(MusicTracks.ExplorationId, playback.MusicIds[^1]);
        host.Flow.Pause(); host.Advance(new(true), 0);
        var before = host.Flow.Snapshot;
        Click("settings"); Click("music");
        Assert.AreSame(before, host.Flow.Snapshot);
        Assert.AreEqual(MusicTracks.OriginalId, host.Flow.UserSettings.MusicTrackId);
        // Выбран Music, неподвижная мышь не отменяет клавиатурный выбор.
        var pointer = host.View.Controls.Single(c => c.Id == "music").Bounds;
        host.Advance(new(true, AppButtons.Left, AppButtons.Left, default,
            new(pointer.X + pointer.Width / 2, pointer.Y + pointer.Height / 2)), 0);
        Assert.AreEqual(MusicTracks.ExplorationId, host.Flow.UserSettings.MusicTrackId);
        Click("back"); Click("continue");
        Assert.AreSame(before, host.Flow.Snapshot);
        Assert.AreEqual(MusicTracks.ExplorationId, playback.MusicIds[^1]);
        host.Flow.Restart(); host.Advance(new(true), 0);
        Assert.AreEqual(MusicTracks.ExplorationId, playback.MusicIds[^1]);
    }

    [TestMethod]
    public void ExternalWaveMatchesTheAuthorsDownloadAndShipsItsNotice()
    {
        string directory = Path.Combine(AppContext.BaseDirectory, "Assets", "Audio");
        Assert.AreEqual("F88993692B978D9BF3D376B1AE45111C1D5F0EA27B4E550E987F580E1384A150",
            Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(directory, "exploration-theme.wav")))));
        StringAssert.Contains(File.ReadAllText(Path.Combine(directory, "exploration-theme-NOTICE.txt")), "Cleyton Kauffman");
        Assert.IsTrue(File.Exists(Path.Combine(directory, "CC0-1.0.txt")));
    }
}
