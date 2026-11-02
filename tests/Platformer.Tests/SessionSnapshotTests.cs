using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Platformer.Core;

namespace Platformer.Tests;

[TestClass]
public sealed class SessionSnapshotTests
{
    private static SessionSnapshot Create(IReadOnlyList<string> coins, IReadOnlyList<HazardPose> poses) =>
        new(Guid.NewGuid(), "level1", 0, 0d, SessionPhase.Running,
            new BallState(Vector3.UnitY, Vector3.Zero, 0.5f, true, Vector3.UnitY, 0f, 0f), coins, poses);

    [TestMethod]
    public void SnapshotCopiesAndSortsBothCollectionsAndExposesReadOnlyLists()
    {
        string[] coins = ["b", "a"];
        var box = new OrientedBox(Vector3.Zero, Vector3.One, Quaternion.Identity);
        var poses = new List<HazardPose> { new("z", box), new("c", box) };
        var snapshot = Create(coins, poses);
        coins[0] = "changed";
        poses.Clear();
        CollectionAssert.AreEqual(new[] { "a", "b" }, snapshot.CollectedCoinIds.ToArray());
        CollectionAssert.AreEqual(new[] { "c", "z" }, snapshot.Hazards.Select(pose => pose.Id).ToArray());
        Assert.AreEqual(2, snapshot.CoinCount);
        Assert.ThrowsExactly<NotSupportedException>(() => ((IList<string>)snapshot.CollectedCoinIds).Clear());
        Assert.ThrowsExactly<NotSupportedException>(() => ((IList<HazardPose>)snapshot.Hazards).Clear());
    }

    [TestMethod]
    public void DuplicateIdsAndInvalidPosesAreRejected()
    {
        Assert.ThrowsExactly<ArgumentException>(() => Create(["coin", "coin"], []));
        Assert.ThrowsExactly<ArgumentException>(() => Create([" "], []));
        Assert.ThrowsExactly<ArgumentException>(() => Create([], [default]));
        var box = new OrientedBox(Vector3.Zero, Vector3.One, Quaternion.Identity);
        Assert.ThrowsExactly<ArgumentException>(() => Create([], [new("h", box), new("h", box)]));
        Assert.ThrowsExactly<ArgumentNullException>(() => Create(null!, []));
        Assert.ThrowsExactly<ArgumentNullException>(() => Create([], null!));
    }

    [TestMethod]
    public void InvalidIdentityTimePhaseAndBallAreRejected()
    {
        var ball = new BallState(Vector3.UnitY, Vector3.Zero, 0.5f, true, Vector3.UnitY, 0f, 0f);
        Guid id = Guid.NewGuid();
        Assert.ThrowsExactly<ArgumentException>(() => new SessionSnapshot(Guid.Empty, "level1", 0, 0d, SessionPhase.Running, ball, [], []));
        Assert.ThrowsExactly<ArgumentException>(() => new SessionSnapshot(id, " ", 0, 0d, SessionPhase.Running, ball, [], []));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new SessionSnapshot(id, "level1", -1, 0d, SessionPhase.Running, ball, [], []));
        foreach (double time in new[] { double.NaN, double.PositiveInfinity, -1d })
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new SessionSnapshot(id, "level1", 0, time, SessionPhase.Running, ball, [], []));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new SessionSnapshot(id, "level1", 0, 0d, (SessionPhase)99, ball, [], []));
        foreach (var invalid in new[] { ball with { Radius = 0f }, ball with { GroundNormal = Vector3.Zero },
            ball with { BoostRemainingSeconds = -1f }, ball with { Velocity = new Vector3(float.NaN) },
            ball with { Center = new Vector3(float.PositiveInfinity) } })
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new SessionSnapshot(id, "level1", 0, 0d, SessionPhase.Running, invalid, [], []));
    }
}
