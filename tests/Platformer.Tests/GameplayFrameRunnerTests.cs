using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Platformer.App;
using Platformer.Core;

namespace Platformer.Tests;

[TestClass]
public sealed class GameplayFrameRunnerTests
{
    private static readonly LevelDefinition EmptyLevel = CameraRigTests.Level();

    private static GameplayFrameRunner Start(bool smoke = false)
    {
        var runner = new GameplayFrameRunner(SimulationSettings.Default, smoke);
        runner.Advance(new(true), 0d, Vector3.Zero, EmptyLevel, _ => Assert.Fail());
        runner.SetScreen(AppScreen.Playing);
        // Первый snapshot после capture не должен давать скачок мыши.
        runner.Advance(new(true, MouseDelta: new(1000f, 1000f)), 0d, Vector3.Zero, EmptyLevel, _ => Assert.Fail());
        Assert.AreEqual(0f, runner.Camera.YawRadians);
        return runner;
    }

    [TestMethod]
    public void MouseChangesMovementInSameFrameOnlyOnceAcrossCatchUpTicks()
    {
        var runner = Start();
        var commands = new List<GameCommand>();
        float pixels = MathF.PI / (2f * CameraRig.MouseSensitivity);
        var result = runner.Advance(new(true, AppButtons.W, AppButtons.Space, new(pixels, -50f)),
            4 * runner.Clock.FixedStepSeconds, Vector3.Zero, EmptyLevel, commands.Add);
        Assert.AreEqual(4, result.Ticks);
        Assert.AreEqual(-MathF.PI / 2f, runner.Camera.YawRadians, 1e-6f);
        Assert.AreEqual(0.55f, runner.Camera.PitchRadians, 1e-6f);
        Assert.IsTrue(commands.All(c => Vector2.Distance(Vector2.UnitX, c.MoveWorldXZ) < 1e-6f));
        Assert.AreEqual(1, commands.Count(c => c.JumpPressed));
    }

    [TestMethod]
    public void PauseRestartAndNextDiscardQueuedEdgesAndFractionBeforeStep()
    {
        foreach (string transition in new[] { "pause", "restart", "next", "focus" })
        {
            var runner = Start();
            double dt = runner.Clock.FixedStepSeconds;
            runner.Advance(new(true, Pressed: AppButtons.Space | AppButtons.LeftShift), dt / 2,
                Vector3.Zero, EmptyLevel, _ => Assert.Fail());
            if (transition == "next") runner.SetScreen(AppScreen.Playing, resetCamera: true);
            else
            {
                var input = transition == "focus" ? new AppInputSnapshot(false)
                    : new AppInputSnapshot(true, Pressed: transition == "pause" ? AppButtons.Escape : AppButtons.R);
                var result = runner.Advance(input, 0.1d, Vector3.Zero, EmptyLevel, _ => Assert.Fail());
                Assert.AreEqual(0, result.Ticks);
                if (transition == "restart") Assert.IsTrue(result.Input.RestartPressed);
            }
            Assert.AreEqual(0d, runner.Clock.AccumulatedSeconds, transition);
            if (transition == "focus") runner.Advance(new(true), 0d, Vector3.Zero, EmptyLevel, _ => Assert.Fail());
            runner.SetScreen(AppScreen.Playing);
            Assert.AreEqual(0, runner.Advance(new(true), dt / 2, Vector3.Zero, EmptyLevel, _ => Assert.Fail()).Ticks);
            runner.Advance(new(true), dt / 2, Vector3.Zero, EmptyLevel,
                command => Assert.AreEqual(default, command, transition));
        }
    }

    [TestMethod]
    public void FocusGainDoesNotResumeAndHeldKeysWaitForRelease()
    {
        var runner = Start();
        Assert.IsTrue(runner.CaptureRequested);
        runner.Advance(new(false), 1d, Vector3.Zero, EmptyLevel, _ => Assert.Fail());
        Assert.AreEqual(AppScreen.Paused, runner.Screen);
        Assert.IsFalse(runner.CaptureRequested);
        var held = new AppInputSnapshot(true, AppButtons.W | AppButtons.Space, AppButtons.Space, new(400f, 400f));
        runner.Advance(held, 1d, Vector3.Zero, EmptyLevel, _ => Assert.Fail());
        Assert.AreEqual(AppScreen.Paused, runner.Screen);
        Assert.IsFalse(runner.CaptureRequested);
        runner.SetScreen(AppScreen.Playing);
        runner.Advance(held, runner.Clock.FixedStepSeconds, Vector3.Zero, EmptyLevel,
            command => Assert.AreEqual(default, command));
        Assert.AreEqual(0f, runner.Camera.YawRadians);
        runner.Advance(held with { Released = held.Down, MouseDelta = Vector2.Zero }, runner.Clock.FixedStepSeconds,
            Vector3.Zero, EmptyLevel, command =>
            {
                Assert.IsTrue(command.JumpPressed);
                Assert.AreEqual(-Vector2.UnitY, command.MoveWorldXZ);
            });
    }

    [TestMethod]
    public void SmokeNeverRequestsCaptureAndMenusNeverStep()
    {
        var runner = Start(smoke: true);
        Assert.IsFalse(runner.CaptureRequested);
        runner.Advance(new(true, MouseDelta: new(500f, 500f)), 0.1d, Vector3.Zero, EmptyLevel, _ => { });
        Assert.AreEqual(0f, runner.Camera.YawRadians);
        foreach (AppScreen screen in Enum.GetValues<AppScreen>().Where(s => s != AppScreen.Playing))
        {
            runner.SetScreen(screen);
            Assert.IsFalse(runner.CaptureRequested);
            Assert.AreEqual(0, runner.Advance(new(true), 100d, Vector3.Zero, EmptyLevel, _ => Assert.Fail()).Ticks);
            Assert.AreEqual(0d, runner.Clock.AccumulatedSeconds);
        }
    }

    [TestMethod]
    public void StepFailureReleasesCaptureAndStopsFollowingFrames()
    {
        var runner = Start();
        Assert.ThrowsExactly<InvalidOperationException>(() => runner.Advance(new(true), 0.1d, Vector3.Zero,
            EmptyLevel, _ => throw new InvalidOperationException("Сеанс не завершил tick")));
        Assert.AreEqual(AppScreen.Failed, runner.Screen);
        Assert.IsFalse(runner.CaptureRequested);
        Assert.IsTrue(runner.Clock.IsFaulted);
        runner.Advance(new(true), 1d, Vector3.Zero, EmptyLevel, _ => Assert.Fail());
    }

    [TestMethod]
    public void RealPhysicsReceivesIdenticalCommandsAtDifferentRenderRates()
    {
        var level = TrainingTrack.Create();
        var settings = SimulationSettings.Default;
        BallState Run(int ticksPerFrame)
        {
            var simulator = new BallSimulator(level, settings);
            var state = simulator.CreateInitialState();
            var runner = Start();
            for (int tick = 0; tick < 30; tick += ticksPerFrame)
                runner.Advance(new(true, AppButtons.D, tick == 0 ? AppButtons.Space : AppButtons.None),
                    ticksPerFrame * runner.Clock.FixedStepSeconds, state.Center + new Vector3(0f, 0.3f, 0f), level,
                    command => state = simulator.Step(state, command).Ball);
            return state;
        }
        Assert.AreEqual(Run(1), Run(2));
        Assert.AreEqual(Run(1), Run(5));
    }
}
