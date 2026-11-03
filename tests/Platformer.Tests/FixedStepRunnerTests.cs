using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Platformer.App;
using Platformer.Core;

namespace Platformer.Tests;

[TestClass]
public sealed class FixedStepRunnerTests
{
    [TestMethod]
    public void ZeroOneAndMultipleTicksKeepEdgesButUseLatestHeld()
    {
        var runner = new FixedStepRunner(SimulationSettings.Default);
        var commands = new List<GameCommand>();
        double dt = runner.FixedStepSeconds;
        Assert.AreEqual(0, runner.Advance(dt / 4, new(Vector2.UnitX, true, false), commands.Add));
        Assert.AreEqual(0, runner.Advance(dt / 4, new(-Vector2.UnitX, false, true), commands.Add));
        Assert.AreEqual(1, runner.Advance(dt / 2, new(Vector2.UnitY, false, false), commands.Add));
        Assert.AreEqual(new GameCommand(Vector2.UnitY, true, true), commands[0]);
        Assert.AreEqual(3, runner.Advance(3 * dt, new(-Vector2.UnitY, false, false), commands.Add));
        Assert.IsTrue(commands.Skip(1).All(command => command == new GameCommand(-Vector2.UnitY, false, false)));
    }

    [TestMethod]
    public void CatchUpConsumesEachEdgeOnlyOnceAndDropsOnlyWholeExcessTicks()
    {
        var runner = new FixedStepRunner(new SimulationSettings(fixedStepSeconds: 1f / 240f));
        var commands = new List<GameCommand>();
        double dt = runner.FixedStepSeconds;
        runner.Advance(dt / 3, default, commands.Add);
        Assert.AreEqual(8, runner.Advance(1d, new(Vector2.Zero, true, true), commands.Add));
        Assert.AreEqual(1, commands.Count(c => c.JumpPressed));
        Assert.AreEqual(1, commands.Count(c => c.BoostPressed));
        Assert.IsTrue(runner.AccumulatedSeconds >= 0d && runner.AccumulatedSeconds < dt);
        Assert.AreEqual(1d + dt / 3, 8 * dt + runner.DroppedSeconds + runner.AccumulatedSeconds, 1e-12d);
    }

    [TestMethod]
    public void CustomStepIsSharedWithSettingsAndClearDiscardsPendingEdgesAndTime()
    {
        var settings = new SimulationSettings(fixedStepSeconds: 1f / 60f);
        var runner = new FixedStepRunner(settings);
        double dt = settings.FixedStepSeconds;
        runner.Advance(dt / 2, new(Vector2.One / MathF.Sqrt(2), true, true), _ => Assert.Fail());
        runner.Clear();
        Assert.AreEqual(0, runner.Advance(dt / 2, default, _ => Assert.Fail()));
        GameCommand received = new(Vector2.UnitX, true, true);
        Assert.AreEqual(1, runner.Advance(dt / 2, default, command => received = command));
        Assert.AreEqual(default, received);
    }

    [TestMethod]
    public void InvalidTimeDoesNotMutateQueueOrAccumulator()
    {
        var runner = new FixedStepRunner(SimulationSettings.Default);
        runner.Advance(runner.FixedStepSeconds / 2, new(Vector2.UnitX, true, false), _ => Assert.Fail());
        foreach (double invalid in new[] { -1d, double.NaN, double.PositiveInfinity, double.NegativeInfinity })
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => runner.Advance(invalid, default, _ => Assert.Fail()));
        Assert.AreEqual(runner.FixedStepSeconds / 2, runner.AccumulatedSeconds);
        Assert.AreEqual(0d, runner.DroppedSeconds);
        runner.Advance(runner.FixedStepSeconds / 2, default, command => Assert.IsTrue(command.JumpPressed));
    }

    [TestMethod]
    public void StepFailureStopsCatchUpAndRequiresExplicitRecovery()
    {
        var runner = new FixedStepRunner(SimulationSettings.Default);
        int calls = 0;
        Assert.ThrowsExactly<InvalidOperationException>(() => runner.Advance(0.1, default, _ =>
        {
            if (++calls == 2) throw new InvalidOperationException("Ошибка сеанса");
        }));
        Assert.AreEqual(2, calls);
        Assert.IsTrue(runner.IsFaulted);
        Assert.ThrowsExactly<InvalidOperationException>(() => runner.Advance(0.1, default, _ => calls++));
        runner.Clear();
        Assert.AreEqual(1, runner.Advance(runner.FixedStepSeconds, default, _ => calls++));
    }
}
