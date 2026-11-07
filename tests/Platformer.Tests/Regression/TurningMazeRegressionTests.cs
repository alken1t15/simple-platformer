using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Platformer.Core;
using static Platformer.Tests.Regression.RegressionFixtures;

namespace Platformer.Tests.Regression;

/// <summary>
/// Независимые регрессии #9 для второй локации (#21): те же проверки монет, финиша и
/// рампы, что для Training Track, на данных level2. Число монет берётся из данных уровня.
/// </summary>
[TestClass]
public sealed class TurningMazeRegressionTests
{
    [TestMethod]
    public void EveryTurningMazeCoinIsTouchedByBallLandingBelowIt()
    {
        // Монеты стоят и над рампой, где шар не обязан замереть, поэтому проверяется
        // первая поза с опорой после вертикального падения.
        var maze = TurningMaze.Create();
        Assert.IsNotEmpty(maze.Coins);
        var simulator = new BallSimulator(maze, Defaults);
        foreach (var coin in maze.Coins)
        {
            var state = Air(coin.Center + Vector3.UnitY * 1.5f);
            for (int tick = 0; tick < 240 && !state.Grounded; tick++)
                state = simulator.Step(state, default).Ball;
            Assert.IsTrue(state.Grounded, $"{coin.Id}: под монетой нет опоры");
            Assert.IsLessThanOrEqualTo(Radius + coin.Radius, Vector3.Distance(state.Center, coin.Center), coin.Id);
        }
    }

    [TestMethod]
    public void BallRestingOnTurningMazeFinishPlatformOverlapsFinishButSpawnDoesNot()
    {
        var maze = TurningMaze.Create();
        var simulator = new BallSimulator(maze, Defaults);
        var state = Air(maze.Finish.Center + Vector3.UnitY * 2f);
        for (int tick = 0; tick < 240 && !(state.Grounded && state.Velocity.LengthSquared() < 1e-8f); tick++)
            state = simulator.Step(state, default).Ball;
        Assert.IsTrue(state.Grounded);
        Assert.IsLessThanOrEqualTo(Radius, Vector3.Distance(state.Center, maze.Finish.ClosestPoint(state.Center)));
        var spawn = simulator.CreateInitialState().Center;
        Assert.IsGreaterThan(Radius, Vector3.Distance(spawn, maze.Finish.ClosestPoint(spawn)));
    }

    [TestMethod]
    public void TurningMazeRampNormalAndUphillDirectionFollowTwentyDegreeGeometry()
    {
        // Из раскладки docs/turning-maze.md: верх рампы идёт от (24,0,8) к (27,3·tan20°,8).
        float angle = 20f * MathF.PI / 180f;
        var normal = new Vector3(-MathF.Sin(angle), MathF.Cos(angle), 0f);
        var foot = new Vector3(24f, 0f, 8f);
        var simulator = new BallSimulator(TurningMaze.Create(), Defaults);
        float x = 24.6f;
        var surface = new Vector3(x, (x - foot.X) * MathF.Tan(angle), foot.Z);
        var state = Air(surface + normal * (Radius + Defaults.SkinWidth / 2f));

        state = simulator.Step(state, default).Ball;
        Assert.IsTrue(state.Grounded);
        Near(normal, state.GroundNormal, NormalTolerance);
        for (int tick = 0; tick < 40; tick++)
        {
            state = simulator.Step(state, Right).Ball;
            Assert.IsTrue(state.Grounded, $"tick {tick}");
            Near(normal, state.GroundNormal, NormalTolerance);
        }
        Assert.IsGreaterThan(0f, state.Velocity.X);
        Assert.AreEqual(MathF.Tan(angle), state.Velocity.Y / state.Velocity.X, AccumulatedTolerance);
        Assert.AreEqual(0f, state.Velocity.Z, DriftTolerance);
        Assert.IsLessThanOrEqualTo(Defaults.MoveSpeed + AccumulatedTolerance, state.Velocity.Length());
        AssertWithinSupportGap(Vector3.Dot(state.Center - foot, normal));
    }
}
