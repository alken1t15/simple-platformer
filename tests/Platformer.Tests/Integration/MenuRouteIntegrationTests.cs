using Microsoft.VisualStudio.TestTools.UnitTesting;
using Platformer.App;
using Platformer.Core;

namespace Platformer.Tests.Integration;

/// <summary>Стык InputRouter → MenuController → LevelFlow → FixedStepRunner (#10).</summary>
[TestClass]
public sealed class MenuRouteIntegrationTests
{
    [TestMethod]
    public void VerticalRouteFromMainMenuThroughBothLevelsEndsInGameComplete()
    {
        var driver = new IntegrationDriver();
        driver.Press(AppButtons.Enter); // Start
        Assert.AreEqual(AppScreen.Playing, driver.Screen);
        Assert.AreEqual(IntegrationDriver.First, driver.Snapshot.LevelId);
        Assert.AreEqual(0L, driver.Snapshot.Tick);

        driver.Press(AppButtons.Escape); // Pause
        Assert.AreEqual(AppScreen.Paused, driver.Screen);
        var paused = driver.Snapshot;
        driver.Click("settings"); // Settings из Pause
        Assert.AreEqual(AppScreen.Settings, driver.Screen);
        driver.Frame(down: AppButtons.D, seconds: 1d);
        driver.Press(AppButtons.Escape); // Back возвращает в Pause, а не в игру
        Assert.AreEqual(AppScreen.Paused, driver.Screen);
        Assert.AreSame(paused, driver.Snapshot, "Settings/Back не должны продвигать сеанс");
        driver.Click("continue");
        Assert.AreEqual(AppScreen.Playing, driver.Screen);
        Assert.AreEqual(paused.SessionId, driver.Snapshot.SessionId);
        Assert.AreEqual(paused.Tick, driver.Snapshot.Tick, "Время паузы и Settings не превращается в ticks");

        driver.HoldUntilLeavesPlaying(AppButtons.D);
        Assert.AreEqual(AppScreen.LevelResult, driver.Screen);
        var firstAttempt = driver.Snapshot.SessionId;
        driver.Click("retry"); // Retry из LevelResult
        Assert.AreEqual(AppScreen.Playing, driver.Screen);
        Assert.AreNotEqual(firstAttempt, driver.Snapshot.SessionId);
        Assert.AreEqual(0L, driver.Snapshot.Tick);
        driver.HoldUntilLeavesPlaying(AppButtons.D);
        driver.Click("next");
        Assert.AreEqual(IntegrationDriver.Second, driver.Snapshot.LevelId);

        driver.HoldUntilLeavesPlaying(AppButtons.A);
        Assert.AreEqual(AppScreen.Failed, driver.Screen);
        driver.Press(AppButtons.R); // Failed/Retry клавишей
        Assert.AreEqual(AppScreen.Playing, driver.Screen);
        Assert.AreEqual(IntegrationDriver.Second, driver.Snapshot.LevelId);
        driver.HoldUntilLeavesPlaying(AppButtons.D);
        driver.Press(AppButtons.Enter); // Next с последнего уровня
        Assert.AreEqual(AppScreen.GameComplete, driver.Screen);
        CollectionAssert.AreEqual(new[] { IntegrationDriver.First, IntegrationDriver.Second },
            driver.Flow.CompletedLevels.Select(result => result.LevelId).ToArray());
        Assert.AreEqual(0, driver.Frame(AppButtons.D, seconds: 1d).Ticks);
        driver.Click("main-menu");
        Assert.AreEqual(AppScreen.MainMenu, driver.Screen);
        Assert.IsNull(driver.Flow.Snapshot);
    }

    [TestMethod]
    public void DirectSecondLevelStartsWithoutFirstLevelResultAndCompletesGame()
    {
        var driver = new IntegrationDriver();
        driver.Click("levels");
        driver.Click($"level:{IntegrationDriver.Second}");
        Assert.AreEqual(IntegrationDriver.Second, driver.Snapshot.LevelId);
        Assert.IsEmpty(driver.Flow.CompletedLevels);
        driver.HoldUntilLeavesPlaying(AppButtons.D);
        driver.Press(AppButtons.Enter);
        Assert.AreEqual(AppScreen.GameComplete, driver.Screen);
        Assert.AreEqual(IntegrationDriver.Second, driver.Flow.CompletedLevels.Single().LevelId);
    }

    [TestMethod]
    public void EnterAndClickInOneFrameOnLevelResultAdvanceExactlyOneLevel()
    {
        var driver = new IntegrationDriver();
        driver.Press(AppButtons.Enter);
        driver.HoldUntilLeavesPlaying(AppButtons.D);
        driver.Click("next", extra: AppButtons.Enter);
        Assert.AreEqual(AppScreen.Playing, driver.Screen, "Двойная активация не должна пропустить level2");
        Assert.AreEqual(IntegrationDriver.Second, driver.Snapshot.LevelId);
        Assert.AreEqual(0L, driver.Snapshot.Tick);
        Assert.HasCount(1, driver.Flow.CompletedLevels);
    }

    [TestMethod]
    public void EnterAndClickInOneFrameOnFailedCreateExactlyOneRetrySession()
    {
        var driver = new IntegrationDriver();
        driver.Click("levels");
        driver.Click($"level:{IntegrationDriver.Second}");
        driver.HoldUntilLeavesPlaying(AppButtons.A);
        Assert.AreEqual(AppScreen.Failed, driver.Screen);
        var failed = driver.Snapshot.SessionId;
        driver.Frame();
        var point = driver.CenterOf("retry");
        driver.Frame(pointer: point);
        driver.Frame(pressed: AppButtons.PointerLeft | AppButtons.Enter | AppButtons.R, pointer: point);
        var retry = driver.Snapshot;
        Assert.AreEqual(AppScreen.Playing, driver.Screen);
        Assert.AreNotEqual(failed, retry.SessionId);
        // Удержание тех же кнопок в Playing не создаёт ещё один сеанс.
        driver.Frame(down: AppButtons.PointerLeft | AppButtons.Enter | AppButtons.R, seconds: driver.FixedStep, pointer: point);
        Assert.AreEqual(retry.SessionId, driver.Snapshot.SessionId);
        Assert.AreEqual(1L, driver.Snapshot.Tick);
    }

    [TestMethod]
    public void ClickOnSettingsWinsOverEnterOnDefaultContinueInSameFrame()
    {
        var driver = new IntegrationDriver();
        driver.Press(AppButtons.Enter);
        driver.Press(AppButtons.Escape);
        driver.Click("settings", extra: AppButtons.Enter);
        Assert.AreEqual(AppScreen.Settings, driver.Screen, "За кадр выполняется одно действие: целевой клик");
    }

    [TestMethod]
    public void ActivationKeyHeldThroughLevelEndDoesNotTriggerNext()
    {
        var driver = new IntegrationDriver();
        driver.Press(AppButtons.Enter);
        // Space — прыжок в игре и активация в меню: удержание через финиш — старая команда.
        for (int i = 0; i < 600 && driver.Screen == AppScreen.Playing; i++)
            driver.Frame(down: AppButtons.D | AppButtons.Space, pressed: i == 0 ? AppButtons.Space : AppButtons.None,
                seconds: driver.FixedStep);
        Assert.AreEqual(AppScreen.LevelResult, driver.Screen);
        for (int i = 0; i < 5; i++) driver.Frame(down: AppButtons.Space, seconds: 0.1d);
        Assert.AreEqual(AppScreen.LevelResult, driver.Screen);
        Assert.AreEqual(IntegrationDriver.First, driver.Snapshot.LevelId);
        driver.Frame(released: AppButtons.Space);
        driver.Press(AppButtons.Space);
        Assert.AreEqual(IntegrationDriver.Second, driver.Snapshot.LevelId);
    }

    [TestMethod]
    public void PauseIsDecidedBeforeTicksSoTerminalFrameCannotSwallowOrDuplicateIt()
    {
        // Финиш на spawn: любой первый tick завершает уровень.
        var driver = new IntegrationDriver(new LevelCatalog([SessionFixtures.Level(finishX: 0f)]));
        driver.Press(AppButtons.Enter);
        var started = driver.Snapshot.SessionId;
        var result = driver.Frame(pressed: AppButtons.Escape | AppButtons.R, seconds: 4 * driver.FixedStep);
        Assert.AreEqual(0, result.Ticks, "Pause применяется до Step, накопленное время отбрасывается");
        Assert.AreEqual(AppScreen.Paused, driver.Screen);
        Assert.AreEqual(started, driver.Snapshot.SessionId, "Pause приоритетнее R в одном кадре");
        Assert.AreEqual(0L, driver.Snapshot.Tick);
        driver.Frame(released: AppButtons.Escape | AppButtons.R);
        driver.Press(AppButtons.Enter); // Continue
        // Escape удерживается с прошлого нажатия: на кадре финиша это не новый фронт.
        driver.Frame(down: AppButtons.Escape, seconds: driver.FixedStep);
        Assert.AreEqual(AppScreen.LevelResult, driver.Screen);
        driver.Frame(down: AppButtons.Escape, seconds: 0.1d);
        Assert.AreEqual(AppScreen.LevelResult, driver.Screen, "Старое удержание Escape не открывает диалог выхода");
        Assert.IsTrue(driver.Host.View.Controls.Any(control => control.Id == "next"));
    }

    [TestMethod]
    public void FocusLossOnEveryScreenClearsPendingCommandsAndNeverResumesByItself()
    {
        var driver = new IntegrationDriver();
        driver.Press(AppButtons.Enter);
        driver.Frame(down: AppButtons.D | AppButtons.Space, pressed: AppButtons.Space | AppButtons.LeftShift,
            focused: false, seconds: 0.5d);
        Assert.AreEqual(AppScreen.Paused, driver.Screen);
        Assert.AreEqual(0L, driver.Snapshot.Tick);
        // Активация окна с зажатыми клавишами: ни продолжения, ни активации Continue.
        driver.Frame(down: AppButtons.Enter | AppButtons.D, pressed: AppButtons.Enter, seconds: 0.5d);
        Assert.AreEqual(AppScreen.Paused, driver.Screen);
        driver.Frame(released: AppButtons.Enter | AppButtons.D);
        driver.Press(AppButtons.Enter);
        Assert.AreEqual(AppScreen.Playing, driver.Screen);
        driver.Frame(seconds: driver.FixedStep);
        Assert.IsFalse(driver.Flow.DrainEvents().Any(e => e.Kind is GameEventKind.Jumped or GameEventKind.BoostStarted),
            "Space/Shift до потери фокуса не должны выполниться после Continue");

        driver.HoldUntilLeavesPlaying(AppButtons.D);
        driver.Frame(focused: false);
        driver.Frame(down: AppButtons.Enter, pressed: AppButtons.Enter);
        Assert.AreEqual(AppScreen.LevelResult, driver.Screen, "Фокус не активирует Next");
    }
}
