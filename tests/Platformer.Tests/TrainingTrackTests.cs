using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Platformer.Core;

namespace Platformer.Tests;

[TestClass]
public sealed class TrainingTrackTests
{
    [TestMethod]
    public void TrackContainsExpectedRouteAndTenCoinsWithoutHazards()
    {
        var level = TrainingTrack.Create();
        Assert.AreEqual("level1", level.Id);
        CollectionAssert.AreEqual(new[] { "start", "ramp", "upper-platform", "bridge", "jump-takeoff",
            "jump-landing", "boost-straight", "finish-platform" }, level.Surfaces.Select(s => s.Id).ToArray());
        Assert.HasCount(10, level.Coins);
        Assert.HasCount(0, level.Hazards);
        Assert.AreEqual(18, level.Surfaces.Select(s => s.Id).Concat(level.Coins.Select(c => c.Id)).Distinct(StringComparer.Ordinal).Count());
        foreach (string section in new[] { "start", "ramp", "bridge", "jump", "boost" })
            Assert.AreEqual(2, level.Coins.Count(c => c.Id.StartsWith($"coin-{section}-", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void SpawnHasSupportForDefaultRadiusAndIsClearOfFinishAndCoins()
    {
        var level = TrainingTrack.Create();
        var start = Surface(level, "start");
        float distance = Vector3.Distance(level.Spawn, start.ClosestPoint(level.Spawn));
        Assert.AreEqual(0.501f, distance, 1e-6f);
        foreach (var surface in level.Surfaces)
            Assert.IsGreaterThanOrEqualTo(0.5f, Vector3.Distance(level.Spawn, surface.Bounds.ClosestPoint(level.Spawn)));
        Assert.IsGreaterThan(0.5f, Vector3.Distance(level.Spawn, level.Finish.ClosestPoint(level.Spawn)));
        foreach (var coin in level.Coins)
            Assert.IsGreaterThan(0.5f + coin.Radius, Vector3.Distance(level.Spawn, coin.Center));
    }

    [TestMethod]
    public void RampEndpointsMeetStartAndUpperPlatformAtTwentyDegrees()
    {
        var level = TrainingTrack.Create();
        var ramp = Surface(level, "ramp");
        var start = Surface(level, "start");
        var upper = Surface(level, "upper-platform");
        Vector3 lowerPoint = ramp.ToWorld(new Vector3(-ramp.HalfExtents.X, ramp.HalfExtents.Y, 0f));
        Vector3 upperPoint = ramp.ToWorld(new Vector3(ramp.HalfExtents.X, ramp.HalfExtents.Y, 0f));
        LevelDefinitionTests.Near(new Vector3(2f, 0f, 0f), lowerPoint);
        LevelDefinitionTests.Near(new Vector3(5f, 3f * MathF.Tan(MathF.PI / 9f), 0f), upperPoint);
        LevelDefinitionTests.Near(lowerPoint, start.ClosestPoint(lowerPoint));
        LevelDefinitionTests.Near(upperPoint, upper.ClosestPoint(upperPoint));
        Assert.AreEqual(2.5f, ramp.HalfExtents.Z * 2f);
        Assert.AreEqual(MathF.Cos(MathF.PI / 9f), Vector3.Transform(Vector3.UnitY, ramp.Orientation).Y, 1e-6f);
    }

    [TestMethod]
    public void BridgeJumpBoostAndFinishMatchLayoutWithoutHiddenWalls()
    {
        var level = TrainingTrack.Create();
        var upper = Surface(level, "upper-platform");
        var bridge = Surface(level, "bridge");
        var takeoff = Surface(level, "jump-takeoff");
        var landing = Surface(level, "jump-landing");
        var boost = Surface(level, "boost-straight");
        var finish = Surface(level, "finish-platform");
        Assert.AreEqual(1.4f, bridge.HalfExtents.X * 2f);
        Assert.AreEqual(6f, bridge.HalfExtents.Z * 2f);
        Assert.AreEqual(upper.Center.Z + upper.HalfExtents.Z, bridge.Center.Z - bridge.HalfExtents.Z);
        Assert.AreEqual(bridge.Center.Z + bridge.HalfExtents.Z, takeoff.Center.Z - takeoff.HalfExtents.Z);
        Assert.AreEqual(1f, landing.Center.Z - landing.HalfExtents.Z - (takeoff.Center.Z + takeoff.HalfExtents.Z));
        Assert.AreEqual(takeoff.Center.Y, landing.Center.Y);
        Assert.AreEqual(8f, boost.HalfExtents.X * 2f);
        Assert.AreEqual(2.5f, boost.HalfExtents.Z * 2f);
        Assert.AreEqual(landing.Center.X - landing.HalfExtents.X, boost.Center.X + boost.HalfExtents.X);
        Assert.AreEqual(boost.Center.X - boost.HalfExtents.X, finish.Center.X + finish.HalfExtents.X);
        float top = finish.Center.Y + finish.HalfExtents.Y;
        LevelDefinitionTests.Near(new Vector3(finish.Center.X, top + 0.5f, finish.Center.Z),
            level.Finish.ClosestPoint(new Vector3(finish.Center.X, top + 0.5f, finish.Center.Z)));
        Assert.IsTrue(level.Surfaces.All(s => s.Bounds == Surface(level, "ramp") || s.Bounds.Orientation == Quaternion.Identity));
    }

    [TestMethod]
    public void CoinsAreAboveSupportedRouteAndOutsideGeometryAndFinish()
    {
        var level = TrainingTrack.Create();
        foreach (var coin in level.Coins)
        {
            foreach (var surface in level.Surfaces)
                Assert.IsGreaterThan(coin.Radius, Vector3.Distance(coin.Center, surface.Bounds.ClosestPoint(coin.Center)), coin.Id);
            Assert.IsGreaterThan(coin.Radius, Vector3.Distance(coin.Center, level.Finish.ClosestPoint(coin.Center)), coin.Id);
            string supportId = coin.Id.Split('-')[1] switch
            {
                "start" => "start", "ramp" => "ramp", "bridge" => "bridge",
                "jump" => coin.Id.EndsWith('1') ? "jump-takeoff" : "jump-landing",
                "boost" => "boost-straight", _ => throw new AssertFailedException(coin.Id),
            };
            var support = Surface(level, supportId);
            var point = support.ClosestPoint(coin.Center);
            Assert.IsLessThan(coin.Center.Y, point.Y);
            // Статическая достижимость сферой радиуса 0.5, не доказательство прохождения физикой
            Vector3 ballCenter = point + Vector3.Transform(Vector3.UnitY, support.Orientation) * 0.501f;
            Assert.IsLessThanOrEqualTo(0.5f + coin.Radius, Vector3.Distance(ballCenter, coin.Center), coin.Id);
        }
    }

    [TestMethod]
    public void KillYIsFiveUnitsBelowLowestStaticVertex()
    {
        var level = TrainingTrack.Create();
        float minimum = float.PositiveInfinity;
        foreach (var surface in level.Surfaces)
        foreach (int x in new[] { -1, 1 })
        foreach (int y in new[] { -1, 1 })
        foreach (int z in new[] { -1, 1 })
            minimum = MathF.Min(minimum, surface.Bounds.ToWorld(surface.Bounds.HalfExtents * new Vector3(x, y, z)).Y);
        Assert.AreEqual(minimum - 5f, level.KillY, 1e-5f);
    }

    private static OrientedBox Surface(LevelDefinition level, string id) => level.Surfaces.Single(s => s.Id == id).Bounds;
}
