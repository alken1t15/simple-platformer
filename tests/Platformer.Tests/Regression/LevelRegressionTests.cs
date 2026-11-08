using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Platformer.Core;
using static Platformer.Tests.Regression.RegressionFixtures;

namespace Platformer.Tests.Regression;

/// <summary>
/// Независимые регрессии #9 для данных уровня: достижимость по реальной физике
/// и неизменяемость, без фиксации числа монет из макета.
/// </summary>
[TestClass]
public sealed class LevelRegressionTests
{
    [TestMethod]
    public void EveryTrainingTrackCoinIsTouchedByBallLandingBelowIt()
    {
        // В момент приземления под монетой шар должен её касаться: расстояние центров
        // ≤ BallRadius + CoinRadius. Число монет берётся из данных уровня.
        var track = TrainingTrack.Create();
        Assert.IsNotEmpty(track.Coins);
        var simulator = new BallSimulator(track, Defaults);
        foreach (var coin in track.Coins)
        {
            var rest = DropOnto(simulator, coin.Center + Vector3.UnitY * 1.5f, coin.Id);
            float reach = Radius + coin.Radius;
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
        Assert.IsLessThanOrEqualTo(Radius, distance);
        // Spawn, напротив, не должен сразу засчитывать финиш.
        var spawn = simulator.CreateInitialState().Center;
        Assert.IsGreaterThan(Radius, Vector3.Distance(spawn, track.Finish.ClosestPoint(spawn)));
    }

    [TestMethod]
    public void SimulationIgnoresLaterChangesOfLevelSourceList()
    {
        // Чёрный ящик в дополнение к структурной проверке #4: после очистки исходного
        // списка поверхностей и добавления в него стены поведение шара не меняется.
        var surfaces = new List<SurfaceDefinition> { Floor() };
        var finish = new OrientedBox(new Vector3(0f, 500f, 0f), Vector3.One, Quaternion.Identity);
        var copied = new LevelDefinition("copy", new Vector3(0f, 0.5005f, 0f), -50f, finish, surfaces, [], []);
        var reference = RunRight(new BallSimulator(copied, Defaults));

        surfaces.Clear();
        surfaces.Add(new SurfaceDefinition("wall", new OrientedBox(new Vector3(1f, 0f, 0f),
            new Vector3(0.1f, 5f, 5f), Quaternion.Identity)));
        Assert.AreEqual(reference, RunRight(new BallSimulator(copied, Defaults)));
        Assert.AreEqual("floor", copied.Surfaces.Single().Id);
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

    private static BallState RunRight(BallSimulator simulator)
    {
        var state = simulator.CreateInitialState();
        for (int tick = 0; tick < 120; tick++) state = simulator.Step(state, Right).Ball;
        return state;
    }
}
