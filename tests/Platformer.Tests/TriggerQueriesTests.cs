using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Platformer.Core;

namespace Platformer.Tests;

[TestClass]
public sealed class TriggerQueriesTests
{
    private static MotionSegment Segment(Vector3 start, Vector3 end) => new(start, end, 0f, 1f);

    [TestMethod]
    public void CoinUsesActualBentPathRatherThanItsChordOrOnlyEndpoint()
    {
        MotionSegment[] path = [new(Vector3.Zero, new(0f, 0f, 3f), 0f, 0.5f),
            new(new(0f, 0f, 3f), new(3f, 0f, 3f), 0.5f, 1f)];
        var onPath = new CoinDefinition("on-path", new(0f, 0f, 1.5f), 0.1f);
        var onChord = new CoinDefinition("on-chord", new(1.5f, 0f, 1.5f), 0.1f);
        Assert.IsTrue(TriggerQueries.Coin(path, onPath, 0.1f));
        Assert.IsFalse(TriggerQueries.Coin(path, onChord, 0.1f));
        Assert.IsTrue(TriggerQueries.Coin([Segment(path[0].Start, path[^1].End)], onChord, 0.1f));
    }

    [TestMethod]
    public void CoinIncludesStationaryAndTangentContactButRejectsNearbyMiss()
    {
        var coin = new CoinDefinition("coin", Vector3.Zero, 0.25f);
        Assert.IsTrue(TriggerQueries.Coin([Segment(new(0.75f, 0f, 0f), new(0.75f, 0f, 0f))], coin, 0.5f));
        Assert.IsTrue(TriggerQueries.Coin([Segment(new(-2f, 0.75f, 0f), new(2f, 0.75f, 0f))], coin, 0.5f));
        Assert.IsFalse(TriggerQueries.Coin([Segment(new(-2f, 0.751f, 0f), new(2f, 0.751f, 0f))], coin, 0.5f));
    }

    [TestMethod]
    public void BoxIncludesThinCrossingInitialOverlapStationaryAndTangency()
    {
        var thin = new OrientedBox(Vector3.Zero, new(0.001f, 1f, 1f), Quaternion.Identity);
        Assert.IsTrue(TriggerQueries.Box([Segment(new(-5f, 0f, 0f), new(5f, 0f, 0f))], thin, 0.1f));
        var box = new OrientedBox(Vector3.Zero, Vector3.One, Quaternion.Identity);
        Assert.IsTrue(TriggerQueries.Box([Segment(Vector3.Zero, Vector3.Zero)], box, 0.5f));
        Assert.IsTrue(TriggerQueries.Box([Segment(new(-3f, 1.5f, 0f), new(3f, 1.5f, 0f))], box, 0.5f));
        Assert.IsFalse(TriggerQueries.Box([Segment(new(-3f, 1.501f, 0f), new(3f, 1.501f, 0f))], box, 0.5f));
        Assert.IsFalse(TriggerQueries.Box([Segment(new(1.4f, 1.4f, 0f), new(1.4f, 1.4f, 0f))], box, 0.5f));
    }

    [TestMethod]
    public void BoxUsesOrientationAndActualSegmentsRatherThanChord()
    {
        var rotated = new OrientedBox(new(5f, 2f, -3f), new(2f, 0.1f, 0.1f),
            Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 4f));
        Assert.IsTrue(TriggerQueries.Box([Segment(rotated.ToWorld(new(-3f, 0f, 0f)),
            rotated.ToWorld(new(3f, 0f, 0f)))], rotated, 0.1f));
        Assert.IsFalse(TriggerQueries.Box([Segment(rotated.ToWorld(new(0f, 1f, 0f)),
            rotated.ToWorld(new(0f, 1f, 0f)))], rotated, 0.1f));
        MotionSegment[] path = [new(Vector3.Zero, new(0f, 0f, 4f), 0f, 0.5f),
            new(new(0f, 0f, 4f), new(4f, 0f, 4f), 0.5f, 1f)];
        var center = new OrientedBox(new(2f, 0f, 2f), new(0.1f), Quaternion.Identity);
        Assert.IsFalse(TriggerQueries.Box(path, center, 0.1f));
        Assert.IsTrue(TriggerQueries.Box([Segment(Vector3.Zero, new(4f, 0f, 4f))], center, 0.1f));
    }
}
