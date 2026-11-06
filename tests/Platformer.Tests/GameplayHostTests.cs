using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Platformer.App;
using Platformer.Core;

namespace Platformer.Tests;

[TestClass]
public sealed class GameplayHostTests
{
    private static GameplayHost Create(params LevelDefinition[] levels) => new(
        new LevelCatalog(levels.Length == 0 ? [SessionFixtures.Level(finishX: 50f)] : levels),
        SimulationSettings.Default, new UserSettings());

    private static void Press(GameplayHost host, AppButtons button)
    {
        host.Advance(new(true), 0d);
        host.Advance(new(true, button, button), 0d);
    }

    private static void Start(GameplayHost host)
    {
        host.Advance(new(true), 0d);
        Press(host, AppButtons.Enter);
        Assert.AreEqual(AppScreen.Playing, host.Flow.Screen);
        host.Advance(new(true), 0d);
    }

    [TestMethod]
    public void StartMouseMovementAndPresenterUseRealSessionAndStableScene()
    {
        var host = Create();
        var scene = host.Scene;
        Assert.IsFalse(host.CaptureRequested);
        Start(host);
        Assert.IsTrue(host.CaptureRequested);
        float pixels = MathF.PI / (2f * CameraRig.MouseSensitivity);
        var result = host.Advance(new(true, AppButtons.W, MouseDelta: new(pixels, 0f)),
            4 * host.Frames.Clock.FixedStepSeconds);
        Assert.AreEqual(4, result.Ticks);
        Assert.IsGreaterThan(0f, host.Flow.Snapshot!.Ball.Center.X);
        Assert.AreEqual(0f, host.Flow.Snapshot.Ball.Center.Z, 1e-5f);
        Assert.AreEqual(host.Flow.Snapshot.Ball.Center, host.Flow.Presenter!.BallObject.Transform.Position);
        Assert.AreSame(scene, host.Scene);
        CollectionAssert.AreEquivalent(host.Flow.Presenter.Scene.Objects.ToArray(), scene.Objects.ToArray());
        host.Camera.Validate();
        Assert.AreEqual(result.Camera!.Value.Position, host.Camera.Position);
        Assert.AreEqual(result.Camera.Value.Target, host.Camera.Target);
    }

    [TestMethod]
    public void FocusPausesWithoutAutomaticResumeAndDiscardsPendingCommands()
    {
        var host = Create();
        Start(host);
        double dt = host.Frames.Clock.FixedStepSeconds;
        host.Advance(new(true, AppButtons.W | AppButtons.Space, AppButtons.Space), dt / 2);
        host.Advance(new(false), 10d);
        Assert.AreEqual(AppScreen.Paused, host.Flow.Screen);
        Assert.IsFalse(host.CaptureRequested);
        long tick = host.Flow.Snapshot!.Tick;
        host.Advance(new(true, AppButtons.W | AppButtons.Space), 10d);
        Assert.AreEqual(tick, host.Flow.Snapshot.Tick);
        Assert.AreEqual(AppScreen.Paused, host.Flow.Screen);
        host.Advance(new(true, AppButtons.W | AppButtons.Space | AppButtons.Enter, AppButtons.Enter), 1d);
        Assert.AreEqual(AppScreen.Playing, host.Flow.Screen);
        host.Advance(new(true, AppButtons.W | AppButtons.Space), dt);
        Assert.AreEqual(Vector2.Zero, host.Frames.Router.Map(new AppInputSnapshot(true, AppButtons.W | AppButtons.Space),
            AppScreen.Playing, 0f).Gameplay.MoveWorldXZ);
        Assert.IsTrue(host.Flow.Snapshot.Ball.Grounded);
        Assert.AreEqual(0f, host.Flow.Snapshot.Ball.Center.X);
        host.Advance(new(true, Released: AppButtons.W | AppButtons.Space), 0d);
        host.Advance(new(true, AppButtons.W | AppButtons.Space, AppButtons.Space), dt);
        Assert.IsGreaterThan(0f, host.Flow.Snapshot.Ball.Velocity.Y);
        Assert.IsLessThan(0f, host.Flow.Snapshot.Ball.Center.Z);
    }

    [TestMethod]
    public void RestartRunsBeforeTicksAndClearsQueuedJumpAndBoost()
    {
        var host = Create();
        Start(host);
        double dt = host.Frames.Clock.FixedStepSeconds;
        host.Advance(new(true, AppButtons.D), dt);
        Guid oldId = host.Flow.Snapshot!.SessionId;
        host.Advance(new(true, Pressed: AppButtons.Space | AppButtons.LeftShift), dt / 2);
        var result = host.Advance(new(true, AppButtons.R, AppButtons.R), 0.1d);
        Assert.AreEqual(0, result.Ticks);
        Assert.AreNotEqual(oldId, host.Flow.Snapshot.SessionId);
        Assert.AreEqual(0L, host.Flow.Snapshot.Tick);
        Assert.AreEqual(0d, host.Frames.Clock.AccumulatedSeconds);
        host.Advance(new(true), dt);
        Assert.IsTrue(host.Flow.Snapshot.Ball.Grounded);
        Assert.IsFalse(host.Flow.DrainEvents().Any(e => e.Kind is GameEventKind.Jumped or GameEventKind.BoostStarted));
    }

    [TestMethod]
    public void TerminalTickStopsCatchUpAndNextUsesNewSceneWithoutStaleCommands()
    {
        var host = Create(SessionFixtures.Level("first", finishX: 0f), SessionFixtures.Level("second", finishX: 50f));
        Start(host);
        var originalScene = host.Scene;
        var oldBall = host.Flow.Presenter!.BallObject;
        var result = host.Advance(new(true, Pressed: AppButtons.LeftShift), 0.1d);
        Assert.AreEqual(1, result.Ticks);
        Assert.AreEqual(1L, host.Flow.Snapshot!.Tick);
        Assert.AreEqual(AppScreen.LevelResult, host.Flow.Screen);
        Assert.AreEqual(AppScreen.LevelResult, host.Frames.Screen);
        Assert.AreEqual(0d, host.Frames.Clock.AccumulatedSeconds);
        Assert.IsFalse(host.CaptureRequested);
        Press(host, AppButtons.Enter);
        Assert.AreEqual("second", host.Flow.Snapshot.LevelId);
        Assert.AreEqual(0L, host.Flow.Snapshot.Tick);
        Assert.AreSame(originalScene, host.Scene);
        Assert.IsFalse(host.Scene.Objects.Contains(oldBall));
        Assert.IsTrue(host.Scene.Objects.Contains(host.Flow.Presenter!.BallObject));
        host.Advance(new(true), host.Frames.Clock.FixedStepSeconds);
        Assert.IsFalse(host.Flow.DrainEvents().Any(e => e.Kind == GameEventKind.BoostStarted));
        host.Camera.Validate();
    }

    [TestMethod]
    public void FailureStopsCatchUpAndRetryIsExplicit()
    {
        var level = SessionFixtures.WithHazards(SessionFixtures.Level(), SessionFixtures.Beam());
        var host = Create(level);
        Start(host);
        Assert.AreEqual(1, host.Advance(new(true), 0.1d).Ticks);
        Assert.AreEqual(AppScreen.Failed, host.Flow.Screen);
        Assert.IsFalse(host.Frames.Clock.IsFaulted);
        Guid previous = host.Flow.Snapshot!.SessionId;
        host.Advance(new(true), 0.1d);
        Assert.AreEqual(1L, host.Flow.Snapshot.Tick);
        Press(host, AppButtons.R);
        Assert.AreNotEqual(previous, host.Flow.Snapshot.SessionId);
        Assert.AreEqual(0L, host.Flow.Snapshot.Tick);
    }

    [TestMethod]
    public void CollectedCoinIsRemovedFromRenderSceneAndRestoredOnRestart()
    {
        var host = Create(SessionFixtures.Level(coins: [new("coin", new(0f, 0.5f, 0f), 0.2f)], finishX: 50f));
        Start(host);
        var coin = host.Flow.Presenter!.CoinObjects["coin"];
        host.Advance(new(true), host.Frames.Clock.FixedStepSeconds);
        Assert.AreEqual(1, host.Flow.Snapshot!.CoinCount);
        Assert.IsFalse(host.Scene.Objects.Contains(coin));
        Press(host, AppButtons.R);
        Assert.AreEqual(0, host.Flow.Snapshot.CoinCount);
        Assert.IsTrue(host.Scene.Objects.Contains(coin));
    }

    [TestMethod]
    public void FullTestRouteEndsInGameCompleteAndNeverResumesTicks()
    {
        var host = Create(SessionFixtures.Level("first", finishX: 0f), SessionFixtures.Level("second", finishX: 0f));
        Start(host);
        Assert.AreEqual(1, host.Advance(new(true), 0.1d).Ticks);
        Press(host, AppButtons.Enter);
        Assert.AreEqual(1, host.Advance(new(true), 0.1d).Ticks);
        Assert.AreEqual(AppScreen.LevelResult, host.Flow.Screen);
        Press(host, AppButtons.Enter);
        Assert.AreEqual(AppScreen.GameComplete, host.Flow.Screen);
        Assert.HasCount(2, host.Flow.CompletedLevels);
        Assert.IsNull(host.Flow.Snapshot);
        Assert.IsFalse(host.CaptureRequested);
        Assert.AreEqual(0, host.Advance(new(true, AppButtons.W), 10d).Ticks);
        Assert.AreEqual(0d, host.Frames.Clock.AccumulatedSeconds);
        Press(host, AppButtons.Enter);
        Assert.AreEqual(AppScreen.MainMenu, host.Flow.Screen);
    }

    [TestMethod]
    public void RealSessionIsIndependentOfRenderTickGrouping()
    {
        BallState Run(int ticksPerFrame)
        {
            var host = Create();
            Start(host);
            for (int tick = 0; tick < 30; tick += ticksPerFrame)
                host.Advance(new(true, AppButtons.D, tick == 0 ? AppButtons.Space : AppButtons.None),
                    ticksPerFrame * host.Frames.Clock.FixedStepSeconds);
            Assert.AreEqual(30L, host.Flow.Snapshot!.Tick);
            return host.Flow.Snapshot.Ball;
        }
        Assert.AreEqual(Run(1), Run(2));
        Assert.AreEqual(Run(1), Run(5));
    }

    [TestMethod]
    public void SmokeNeverCapturesAndBackReleasesCursorBeforeClose()
    {
        var host = new GameplayHost(new LevelCatalog([TrainingTrack.Create()]), SimulationSettings.Default,
            new UserSettings(), smoke: true);
        Start(host);
        Assert.IsFalse(host.CaptureRequested);
        Press(host, AppButtons.Escape);
        Assert.AreEqual(AppScreen.Paused, host.Flow.Screen);
        Press(host, AppButtons.Escape);
        Assert.AreEqual(AppScreen.MainMenu, host.Flow.Screen);
        Press(host, AppButtons.Escape);
        Assert.IsTrue(host.CloseRequested);
        Assert.IsFalse(host.CaptureRequested);
    }
}
