using Microsoft.VisualStudio.TestTools.UnitTesting;
using Platformer.App;
using Platformer.Core;

namespace Platformer.Tests.Integration;

/// <summary>
/// Стык GameSession events → GameplayHost → AudioCoordinator → backend double (#10).
/// Проверяет число и lifecycle voices; слышимость — только native Linux прогон.
/// </summary>
[TestClass]
public sealed class AudioLifecycleIntegrationTests
{
    [TestMethod]
    public void EachEventPlaysItsClipOnceAcrossFramesPauseAndContinue()
    {
        var driver = new IntegrationDriver();
        driver.Press(AppButtons.Enter);
        driver.Frame(seconds: driver.FixedStep);
        Assert.AreEqual(1, driver.Playback.Count(GameEventKind.CoinCollected));
        for (int i = 0; i < 3; i++)
        {
            driver.Press(AppButtons.Escape);
            driver.Press(AppButtons.Enter);
            driver.Frame(seconds: driver.FixedStep);
        }
        Assert.AreEqual(1, driver.Playback.Count(GameEventKind.CoinCollected));
        driver.Frame(down: AppButtons.Space, pressed: AppButtons.Space, seconds: driver.FixedStep);
        Assert.AreEqual(1, driver.Playback.Count(GameEventKind.Jumped));
        int effectsBeforeBoost = driver.Playback.Effects.Count;
        driver.Frame(released: AppButtons.Space);
        driver.Frame(down: AppButtons.LeftShift, pressed: AppButtons.LeftShift, seconds: driver.FixedStep);
        Assert.IsGreaterThan(0f, driver.Snapshot.Ball.BoostRemainingSeconds, "Boost действительно запущен");
        Assert.HasCount(effectsBeforeBoost, driver.Playback.Effects, "BoostStarted не имеет клипа");
        driver.Frame(released: AppButtons.LeftShift);
        driver.HoldUntilLeavesPlaying(AppButtons.D);
        Assert.AreEqual(1, driver.Playback.Count(GameEventKind.LevelCompleted));
        for (int i = 0; i < 5; i++) driver.Frame(seconds: 0.1d);
        Assert.AreEqual(1, driver.Playback.Count(GameEventKind.LevelCompleted));
    }

    [TestMethod]
    public void MusicLoopIsSingleAndPausedThroughPauseSettingsAndFocusLoss()
    {
        var driver = new IntegrationDriver();
        driver.Press(AppButtons.Enter);
        var music = driver.Playback.Music.Single();
        Assert.IsFalse(music.Paused);
        driver.Press(AppButtons.Escape);
        Assert.IsTrue(music.Paused);
        driver.Click("settings");
        Assert.IsTrue(music.Paused, "Settings из Pause сохраняет паузу звука");
        driver.Press(AppButtons.Escape);
        driver.Click("continue");
        Assert.IsFalse(music.Paused);
        driver.Frame(focused: false);
        Assert.IsTrue(music.Paused);
        driver.Frame();
        Assert.IsTrue(music.Paused, "Активация окна не продолжает звук без Continue");
        driver.Press(AppButtons.Enter);
        Assert.IsFalse(music.Paused);
        Assert.AreSame(music, driver.Playback.Music.Single(), "Continue не запускает второй loop");
    }

    [TestMethod]
    public void NewSessionEventsPlayAfterRestartAndLateOldSessionEventsAreIgnored()
    {
        var driver = new IntegrationDriver();
        driver.Press(AppButtons.Enter);
        var oldSession = driver.Snapshot.SessionId;
        driver.Frame(seconds: driver.FixedStep);
        driver.Frame(down: AppButtons.Space, pressed: AppButtons.Space, seconds: driver.FixedStep);
        Assert.AreEqual(1, driver.Playback.Count(GameEventKind.CoinCollected));
        var previousVoices = driver.Playback.All.ToArray();

        driver.Press(AppButtons.R);
        Assert.AreNotEqual(oldSession, driver.Snapshot.SessionId);
        Assert.IsTrue(previousVoices.All(voice => voice.Disposed), "Restart закрывает voices прежней попытки");
        Assert.AreEqual("music", driver.Playback.Live.Single().Clip, "Новая попытка — один новый loop");
        // В новом сеансе Sequence снова начинается с 1: высокий Sequence старого сеанса не подавляет её.
        driver.Frame(seconds: driver.FixedStep);
        Assert.AreEqual(2, driver.Playback.Count(GameEventKind.CoinCollected));

        int effects = driver.Playback.Effects.Count;
        driver.Audio.Consume(driver.Snapshot.SessionId,
            [new GameEvent(oldSession, 99, 99, GameEventKind.Failed, null)]);
        Assert.HasCount(effects, driver.Playback.Effects, "Запоздавшее событие старого SessionId не звучит");
        driver.Audio.Consume(driver.Snapshot.SessionId,
            [new GameEvent(driver.Snapshot.SessionId, 1, 1, GameEventKind.CoinCollected, "spawn-coin")]);
        Assert.HasCount(effects, driver.Playback.Effects, "Повторная доставка уже сыгранного Sequence не звучит");
    }

    [TestMethod]
    public void NextMainMenuAndExitCloseEveryVoiceAndPlayback()
    {
        var driver = new IntegrationDriver();
        driver.Press(AppButtons.Enter);
        driver.HoldUntilLeavesPlaying(AppButtons.D);
        Assert.IsTrue(driver.Playback.Music.Single().Disposed, "Музыка закрыта на экране результата");
        driver.Press(AppButtons.Enter); // Next
        Assert.AreEqual(1, driver.Playback.Live.Count(), "После Next жив только новый loop");
        Assert.AreEqual("music", driver.Playback.Live.Single().Clip);

        driver.Frame(down: AppButtons.Space, pressed: AppButtons.Space, seconds: driver.FixedStep);
        var jump = driver.Playback.Effects.Single(voice => voice.Clip == nameof(GameEventKind.Jumped) && !voice.Disposed);
        driver.Frame(released: AppButtons.Space);
        driver.Press(AppButtons.Escape);
        Assert.IsTrue(jump.Paused, "Эффект в полёте ставится на паузу вместе с музыкой");
        driver.Click("main-menu");
        driver.Click("confirm-main-menu");
        Assert.AreEqual(AppScreen.MainMenu, driver.Screen);
        Assert.IsEmpty(driver.Playback.Live, "Выход в меню закрывает voices");

        driver.Press(AppButtons.Enter);
        Assert.AreEqual(1, driver.Playback.Live.Count());
        driver.Audio.Dispose(); // Program: using gameAudio при выходе из Engine.Run
        Assert.IsEmpty(driver.Playback.Live);
        Assert.IsTrue(driver.Playback.Disposed, "Клипы backend освобождаются при выходе");
    }
}
