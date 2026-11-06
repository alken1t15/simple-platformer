using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Platformer.Core;

namespace Platformer.Tests;

[TestClass]
public sealed class TurningMazeTests
{
    [TestMethod]
    public void LevelIsValidAndSessionStartsWithExpectedObjects()
    {
        var level = TurningMaze.Create();
        Assert.AreEqual("level2", level.Id);
        Assert.HasCount(15, level.Coins);
        Assert.AreEqual(3, level.Coins.Count(coin => coin.Id.StartsWith("bonus-", StringComparison.Ordinal)));
        Assert.HasCount(1, level.Hazards);
        var session = new GameSession(level, SimulationSettings.Default);
        Assert.AreEqual(SessionPhase.Running, session.Snapshot.Phase);
        Assert.AreEqual(level.Spawn, session.Snapshot.Ball.Center);
        Assert.HasCount(1, session.Snapshot.Hazards);
        session.Step(new GameCommand(Vector2.Zero, false, false));
        Assert.AreEqual(SessionPhase.Running, session.Snapshot.Phase);
    }

    [TestMethod]
    public void CoinsHaveSupportAndDoNotOverlapSpawnSurfaceOrFinish()
    {
        var level = TurningMaze.Create();
        foreach (var coin in level.Coins)
        {
            Assert.IsGreaterThan(0.5f + coin.Radius, Vector3.Distance(level.Spawn, coin.Center), coin.Id);
            Assert.IsGreaterThan(coin.Radius, Vector3.Distance(coin.Center, level.Finish.ClosestPoint(coin.Center)), coin.Id);
            foreach (var surface in level.Surfaces)
                Assert.IsGreaterThan(coin.Radius, Vector3.Distance(coin.Center, surface.Bounds.ClosestPoint(coin.Center)), coin.Id);
            Assert.IsTrue(level.Surfaces.Any(surface =>
            {
                var closest = surface.Bounds.ClosestPoint(coin.Center);
                return closest.Y < coin.Center.Y && Vector3.Distance(closest, coin.Center) < 0.9f;
            }), coin.Id);
        }
    }

    [TestMethod]
    public void JumpHasOneUnitGapAndRampMeetsBothPlatforms()
    {
        var level = TurningMaze.Create();
        var arena = level.Surfaces.Single(surface => surface.Id == "arena").Bounds;
        var ramp = level.Surfaces.Single(surface => surface.Id == "ramp").Bounds;
        var takeoff = level.Surfaces.Single(surface => surface.Id == "takeoff").Bounds;
        var landing = level.Surfaces.Single(surface => surface.Id == "landing").Bounds;
        var start = ramp.ToWorld(new Vector3(-ramp.HalfExtents.X, ramp.HalfExtents.Y, 0));
        var end = ramp.ToWorld(new Vector3(ramp.HalfExtents.X, ramp.HalfExtents.Y, 0));
        LevelDefinitionTests.Near(start, arena.ClosestPoint(start));
        LevelDefinitionTests.Near(end, takeoff.ClosestPoint(end));
        Assert.AreEqual(1f, landing.Center.X - landing.HalfExtents.X - takeoff.Center.X - takeoff.HalfExtents.X);
        Assert.AreEqual(takeoff.Center.Y, landing.Center.Y);
    }
}
