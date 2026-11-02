using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Platformer.Core;

namespace Platformer.Tests.Regression;

/// <summary>
/// Независимые регрессии #9 для данных уровня: достижимость по реальной физике
/// и неизменяемость, без фиксации числа монет из макета.
/// </summary>
[TestClass]
public sealed class LevelRegressionTests
{
    private static readonly SimulationSettings Defaults = SimulationSettings.Default;

    [TestMethod]
    public void EveryTrainingTrackCoinIsTouchedByBallRestingBelowIt()
    {
        // Шар, опущенный на опору под монетой, должен её касаться: расстояние центров
        // ≤ BallRadius + CoinRadius. Число монет берётся из данных уровня.
        var track = TrainingTrack.Create();
        Assert.IsNotEmpty(track.Coins);
        var simulator = new BallSimulator(track, Defaults);
        foreach (var coin in track.Coins)
        {
            var rest = DropOnto(simulator, coin.Center + Vector3.UnitY * 1.5f, coin.Id);
            float reach = Defaults.BallRadius + coin.Radius;
            Assert.IsLessThanOrEqualTo(reach, Vector3.Distance(rest.Center, coin.Center), coin.Id);
        }
    }

    [TestMethod]
    public void BallRestingOnFinishPlatformOverlapsFinishVolume()
    {
        var track = TrainingTrack.Create();
        var simulator = new BallSimulator(track, Defaults);
        Vector3 center = track.Finish.Center;
        var rest = DropOnto(simulator, center + Vector3.UnitY * 2f, "finish");
        float distance = Vector3.Distance(rest.Center, track.Finish.ClosestPoint(rest.Center));
        Assert.IsLessThanOrEqualTo(Defaults.BallRadius, distance);
        // Spawn, напротив, не должен сразу засчитывать финиш.
        var spawn = simulator.CreateInitialState().Center;
        Assert.IsGreaterThan(Defaults.BallRadius, Vector3.Distance(spawn, track.Finish.ClosestPoint(spawn)));
    }

    [TestMethod]
    public void TrainingTrackFactoryReturnsIndependentEquivalentLevels()
    {
        var first = TrainingTrack.Create();
        var second = TrainingTrack.Create();
        Assert.AreNotSame(first, second);
        CollectionAssert.AreEqual(first.Surfaces.Select(s => s.Id).ToArray(), second.Surfaces.Select(s => s.Id).ToArray());
        CollectionAssert.AreEqual(first.Coins.Select(c => c.Center).ToArray(), second.Coins.Select(c => c.Center).ToArray());
        Assert.AreEqual(first.Spawn, second.Spawn);
        Assert.AreEqual(first.Finish.Center, second.Finish.Center);
    }

    private static BallState DropOnto(BallSimulator simulator, Vector3 start, string label)
    {
        var state = new BallState(start, Vector3.Zero, Defaults.BallRadius, false, Vector3.Zero, 0f, 0f);
        for (int tick = 0; tick < 240; tick++)
        {
            state = simulator.Step(state, default).Ball;
            if (state.Grounded && state.Velocity.LengthSquared() < 1e-8f) return state;
        }
        Assert.Fail($"{label}: шар не нашёл опору под точкой");
        return state;
    }
}
