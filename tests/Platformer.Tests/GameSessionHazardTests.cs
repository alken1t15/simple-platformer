using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Platformer.Core;
using static Platformer.Tests.SessionFixtures;

namespace Platformer.Tests;

[TestClass]
public sealed class GameSessionHazardTests
{
    [TestMethod]
    public void SafeTicksPublishSortedImmutablePosesAndRestartRestoresInitialPhase()
    {
        var level = WithHazards(SessionFixtures.Level(finishX: 50f),
            Beam("z", new(10f, 1f, 0f), speed: 1f, offset: Vector3.UnitX),
            Beam("a", new(15f, 1f, 0f), speed: -2f, angle: 0.4f));
        var session = new GameSession(level, SimulationSettings.Default);
        var initial = session.Snapshot;
        CollectionAssert.AreEqual(new[] { "a", "z" }, initial.Hazards.Select(pose => pose.Id).ToArray());
        Assert.ThrowsExactly<NotSupportedException>(() => ((IList<HazardPose>)initial.Hazards).Clear());
        for (int i = 0; i < 20; i++) session.Step(default);
        Assert.AreEqual(SessionPhase.Running, session.Snapshot.Phase);
        Assert.IsEmpty(session.DrainEvents());
        foreach (var hazard in level.Hazards)
        {
            AssertPose(HazardMotion.Evaluate(hazard, 0d), initial.Hazards.Single(pose => pose.Id == hazard.Id));
            AssertPose(HazardMotion.Evaluate(hazard, session.Snapshot.SimulationSeconds),
                session.Snapshot.Hazards.Single(pose => pose.Id == hazard.Id));
        }
        var previous = session.Snapshot;
        session.Restart();
        Assert.AreNotEqual(previous.SessionId, session.Snapshot.SessionId);
        Assert.AreEqual(0, session.Snapshot.Tick);
        Assert.AreEqual(0d, session.Snapshot.SimulationSeconds);
        Assert.AreEqual(initial.Ball, session.Snapshot.Ball);
        Assert.IsEmpty(session.LastPath);
        foreach (var pose in initial.Hazards)
            AssertPose(pose, session.Snapshot.Hazards.Single(item => item.Id == pose.Id));
        Assert.AreEqual(20, previous.Tick);
        foreach (var hazard in level.Hazards)
            AssertPose(HazardMotion.Evaluate(hazard, previous.SimulationSeconds),
                previous.Hazards.Single(pose => pose.Id == hazard.Id));
    }

    [TestMethod]
    public void MultipleHazardContactsDominateCoinAndFinishAndProduceOneTerminalEvent()
    {
        var level = WithHazards(SessionFixtures.Level(finishX: 0f,
            coins: [new("coin", new(0f, 0.501f, 0f), 0.1f)]), Beam("z"), Beam("a"));
        var session = new GameSession(level, SimulationSettings.Default);
        session.Step(default);
        var final = session.Snapshot;
        var path = session.LastPath;
        Assert.AreEqual(SessionPhase.Failed, final.Phase);
        Assert.AreEqual(0, final.CoinCount);
        Assert.AreEqual(1, final.Tick);
        Assert.AreEqual((double)SimulationSettings.Default.FixedStepSeconds, final.SimulationSeconds);
        session.Step(default);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => session.Step(new(new(float.NaN, 0f), false, false)));
        Assert.AreSame(final, session.Snapshot);
        Assert.AreSame(path, session.LastPath);
        var failure = session.DrainEvents().Single();
        Assert.AreEqual(GameEventKind.Failed, failure.Kind);
        Assert.AreEqual(final.SessionId, failure.SessionId);
        Assert.AreEqual(1, failure.Sequence);
        Assert.AreEqual(1, failure.Tick);
        Assert.IsEmpty(session.DrainEvents());
    }

    [TestMethod]
    public void RotatingBeamHitsStationaryBallBetweenEndpointPosesOnTheSecondTick()
    {
        var settings = SimulationSettings.Default;
        var level = RotatingContactLevel(settings);
        var hazard = level.Hazards.Single();
        var session = new GameSession(level, settings);
        var point = session.Snapshot.Ball.Center;
        session.Step(default);
        Assert.AreEqual(SessionPhase.Running, session.Snapshot.Phase);
        Assert.AreEqual(point, session.Snapshot.Ball.Center);
        foreach (double seconds in new[] { (double)settings.FixedStepSeconds, 2d * settings.FixedStepSeconds })
            Assert.IsGreaterThan(settings.BallRadius,
                Vector3.Distance(point, HazardMotion.Evaluate(hazard, seconds).Bounds.ClosestPoint(point)));
        session.Step(default);
        Assert.AreEqual(point, session.Snapshot.Ball.Center);
        Assert.AreEqual(SessionPhase.Failed, session.Snapshot.Phase);
        Assert.AreEqual(2, session.Snapshot.Tick);
        Assert.AreEqual(2, session.DrainEvents().Single().Tick);
        AssertPose(HazardMotion.Evaluate(hazard, 2d * settings.FixedStepSeconds), session.Snapshot.Hazards.Single());
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ContactUsesTheBentPathInsteadOfItsChord(bool onPath)
    {
        var settings = new SimulationSettings(maxCollisionSubsteps: 128);
        Vector3 pivot = onPath ? new(0f, 0.501f, 1f) : new(1f, 0.501f, 1f);
        var hazard = Beam(pivot: pivot, halfExtents: new(0.1f));
        var level = WithHazards(SessionFixtures.Level(finishX: 50f), hazard);
        Vector3 corner = new(0f, 0.501f, 2f), end = new(2f, 0.501f, 2f);
        var session = new GameSession(level, settings, (ball, _) =>
            new BallStepResult(ball with { Center = end },
                [new(ball.Center, corner, 0f, 0.5f), new(corner, end, 0.5f, 1f)]));
        Assert.AreEqual(!onPath, HazardCollision.IntersectsPath(hazard,
            [new(session.Snapshot.Ball.Center, end, 0f, 1f)], 0d, settings), "Fixture must distinguish Path from its chord");
        session.Step(default);
        Assert.AreEqual(onPath ? SessionPhase.Failed : SessionPhase.Running, session.Snapshot.Phase);
        Assert.HasCount(2, session.LastPath);
    }

    [TestMethod]
    public void HazardBudgetFailurePreservesSnapshotPathPosesAndUndrainedSequence()
    {
        var settings = new SimulationSettings(moveSpeed: 0.1f, jumpSpeed: 0.1f,
            boostMultiplier: 1f, maxFallSpeed: 0.1f, maxCollisionSubsteps: 1);
        var basis = SessionFixtures.Level(finishX: 50f);
        var spawn = new Vector3(2.51f, 0.501f, 0f);
        var level = new LevelDefinition("budget", spawn, basis.KillY, basis.Finish, basis.Surfaces,
            [new("coin", spawn, 0.1f)], [Beam(speed: 1f)]);
        int calls = 0;
        var session = new GameSession(level, settings, (ball, _) =>
        {
            Vector3 end = ++calls == 2 ? ball.Center - new Vector3(0.003f, 0f, 0f) : ball.Center;
            return new BallStepResult(ball with { Center = end }, [new(ball.Center, end, 0f, 1f)]);
        });
        session.Step(default);
        var before = session.Snapshot;
        var path = session.LastPath;
        var error = Assert.ThrowsExactly<InvalidOperationException>(() => session.Step(new(Vector2.Zero, true, true)));
        StringAssert.Contains(error.Message, "beam");
        Assert.AreSame(before, session.Snapshot);
        Assert.AreSame(path, session.LastPath);
        session.Step(new(Vector2.Zero, true, false));
        Assert.AreEqual(2, session.Snapshot.Tick);
        var events = session.DrainEvents();
        CollectionAssert.AreEqual(new[] { GameEventKind.CoinCollected, GameEventKind.Jumped }, events.Select(item => item.Kind).ToArray());
        CollectionAssert.AreEqual(new long[] { 1, 2 }, events.Select(item => item.Sequence).ToArray());
        CollectionAssert.AreEqual(new long[] { 1, 2 }, events.Select(item => item.Tick).ToArray());
        AssertPose(HazardMotion.Evaluate(level.Hazards[0], before.SimulationSeconds), before.Hazards.Single());
    }

    [TestMethod]
    public void InvalidHazardPoseDoesNotPublishPartOfTheNextTick()
    {
        var settings = SimulationSettings.Default;
        var hazard = Beam(pivot: new(9999f, 2f, 0f), offset: new(2f, 0f, 0f), speed: -1f,
            angle: (float)(Math.PI / 3d + 1.5d * settings.FixedStepSeconds));
        var level = WithHazards(SessionFixtures.Level(finishX: 50f,
            coins: [new("coin", new(0f, 0.501f, 0f), 0.1f)]), hazard);
        var session = new GameSession(level, settings);
        session.Step(default);
        var before = session.Snapshot;
        var path = session.LastPath;
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => session.Step(default));
        Assert.AreSame(before, session.Snapshot);
        Assert.AreSame(path, session.LastPath);
        Assert.AreEqual(GameEventKind.CoinCollected, session.DrainEvents().Single().Kind);
        Assert.AreEqual(1, session.Snapshot.Tick);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void RealContactKeepsEarlierCoinsAndRestartClearsTheFailedAttempt(bool drainBeforeRestart)
    {
        var level = WithHazards(SessionFixtures.Level(finishX: 50f,
            coins: [new("coin", new(0f, 0.501f, 0f), 0.1f)]),
            Beam(pivot: new(1.25f, 0.501f, 0f), halfExtents: new(0.1f, 0.2f, 1f)));
        var session = new GameSession(level, SimulationSettings.Default);
        var initial = session.Snapshot;
        session.Step(default);
        SessionFixtures.Until(() => session.Snapshot.Phase == SessionPhase.Failed, () => session.Step(SessionFixtures.Right));
        var failed = session.Snapshot;
        Assert.AreEqual(1, failed.CoinCount);
        if (drainBeforeRestart)
        {
            var events = session.DrainEvents();
            CollectionAssert.AreEqual(new[] { GameEventKind.CoinCollected, GameEventKind.Failed }, events.Select(item => item.Kind).ToArray());
            CollectionAssert.AreEqual(new long[] { 1, 2 }, events.Select(item => item.Sequence).ToArray());
        }
        session.Restart();
        Assert.AreNotEqual(failed.SessionId, session.Snapshot.SessionId);
        Assert.AreEqual(initial.Ball, session.Snapshot.Ball);
        Assert.AreEqual(0, session.Snapshot.CoinCount);
        Assert.AreEqual(SessionPhase.Running, session.Snapshot.Phase);
        Assert.IsEmpty(session.DrainEvents());
        session.Step(default);
        var recollected = session.DrainEvents().Single();
        Assert.AreEqual(GameEventKind.CoinCollected, recollected.Kind);
        Assert.AreEqual(1, recollected.Sequence);
        Assert.AreEqual(session.Snapshot.SessionId, recollected.SessionId);
        Assert.AreEqual(1, failed.CoinCount);
    }
}
