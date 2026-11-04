using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Platformer.Core;

namespace Platformer.Tests;

[TestClass]
public sealed class GameSessionTests
{
    [TestMethod]
    public void RealPhysicsCollectsOnceInOrdinalOrderAndSnapshotEventsStayImmutable()
    {
        var level = SessionFixtures.Level(coins: [new("z", new(0f, 0.501f, 0f), 0.1f), new("a", new(0f, 0.501f, 0f), 0.1f)]);
        var session = new GameSession(level, SimulationSettings.Default);
        var initial = session.Snapshot;
        session.Step(default);
        var first = session.Snapshot;
        var events = session.DrainEvents();
        CollectionAssert.AreEqual(new[] { "a", "z" }, first.CollectedCoinIds.ToArray());
        CollectionAssert.AreEqual(new[] { "a", "z" }, events.Select(item => item.EntityId).ToArray());
        CollectionAssert.AreEqual(new long[] { 1, 2 }, events.Select(item => item.Sequence).ToArray());
        Assert.IsTrue(events.All(item => item.Tick == 1 && item.SessionId == first.SessionId));
        Assert.IsEmpty(session.DrainEvents());
        for (int i = 0; i < 20; i++) session.Step(default);
        Assert.IsEmpty(session.DrainEvents());
        Assert.AreEqual(0, initial.Tick);
        Assert.AreEqual(0, initial.CoinCount);
        Assert.AreEqual(1, first.Tick);
        Assert.HasCount(2, events);
        Assert.ThrowsExactly<NotSupportedException>(() => ((IList<GameEvent>)events).Clear());
    }

    [TestMethod]
    public void AcceptedJumpAndBoostProduceEventsButRepeatedAirPressDoesNot()
    {
        var session = new GameSession(SessionFixtures.Level(finishX: 50f), SimulationSettings.Default);
        var command = new GameCommand(Vector2.Zero, true, true);
        session.Step(command);
        CollectionAssert.AreEqual(new[] { GameEventKind.BoostStarted, GameEventKind.Jumped },
            session.DrainEvents().Select(item => item.Kind).ToArray());
        session.Step(command);
        Assert.IsEmpty(session.DrainEvents());
        Assert.IsFalse(session.Snapshot.Ball.Grounded);
    }

    [TestMethod]
    public void SessionCollectsBentPathAndDoesNotCollectAcrossItsChord()
    {
        var level = SessionFixtures.Level(coins: [new("path", new(0f, 0.501f, 2f), 0.1f),
            new("chord", new(2f, 0.501f, 2f), 0.1f)], finishX: 50f);
        var session = new GameSession(level, SimulationSettings.Default, (ball, _) =>
            new BallStepResult(ball with { Center = new(4f, 0.501f, 4f) },
                [new(ball.Center, new(0f, 0.501f, 4f), 0f, 0.5f), new(new(0f, 0.501f, 4f), new(4f, 0.501f, 4f), 0.5f, 1f)]));
        session.Step(default);
        CollectionAssert.AreEqual(new[] { "path" }, session.Snapshot.CollectedCoinIds.ToArray());
    }

    [TestMethod]
    public void KillYDominatesCoinsAndFinishInTheSameTick()
    {
        var level = new LevelDefinition("fall", Vector3.UnitY, -1f,
            new OrientedBox(Vector3.Zero, Vector3.One, Quaternion.Identity), [], [new("coin", Vector3.Zero, 0.1f)], []);
        var session = new GameSession(level, SimulationSettings.Default, (ball, _) =>
            new BallStepResult(ball with { Center = new(0f, -2f, 0f), Grounded = false, GroundNormal = Vector3.Zero },
                [new(ball.Center, new(0f, -2f, 0f), 0f, 1f)]));
        session.Step(default);
        Assert.AreEqual(SessionPhase.Failed, session.Snapshot.Phase);
        Assert.AreEqual(0, session.Snapshot.CoinCount);
        Assert.AreEqual(GameEventKind.Failed, session.DrainEvents().Single().Kind);
        Assert.AreEqual(1, session.Snapshot.Tick);
    }

    [TestMethod]
    public void StationaryFinishIsTerminalButStillValidatesFutureCommands()
    {
        var session = new GameSession(SessionFixtures.Level(finishX: 0f), SimulationSettings.Default);
        session.Step(default);
        var final = session.Snapshot;
        Assert.AreEqual(SessionPhase.LevelComplete, final.Phase);
        Assert.AreEqual(GameEventKind.LevelCompleted, session.DrainEvents().Single().Kind);
        session.Step(default);
        Assert.AreSame(final, session.Snapshot);
        Assert.IsEmpty(session.DrainEvents());
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => session.Step(new(new(float.NaN, 0f), false, false)));
        Assert.AreSame(final, session.Snapshot);
    }

    [TestMethod]
    public void CoinBeforeFinishDoesNotGateCompletionAndEventsAreDeliveredOnlyOnce()
    {
        var level = SessionFixtures.Level(finishX: 0f, coins: [new("near", new(0f, 0.501f, 0f), 0.1f),
            new("far", new(20f, 0.5f, 0f), 0.1f)]);
        var session = new GameSession(level, SimulationSettings.Default);
        session.Step(default);
        Assert.AreEqual(SessionPhase.LevelComplete, session.Snapshot.Phase);
        Assert.AreEqual(1, session.Snapshot.CoinCount);
        CollectionAssert.AreEqual(new[] { GameEventKind.CoinCollected, GameEventKind.LevelCompleted },
            session.DrainEvents().Select(item => item.Kind).ToArray());
        session.Step(default);
        Assert.IsEmpty(session.DrainEvents());
    }

    [TestMethod]
    public void SimulationFailureRollsBackSnapshotTimeCoinsAndQueuedSequence()
    {
        var level = SessionFixtures.Level(coins: [new("coin", new(0f, 0.501f, 0f), 0.1f)], finishX: 50f);
        var simulator = new BallSimulator(level, SimulationSettings.Default);
        int calls = 0;
        var session = new GameSession(level, SimulationSettings.Default, (ball, command) =>
            ++calls == 2 ? throw new InvalidOperationException("surface budget") : simulator.Step(ball, command));
        session.Step(default);
        var before = session.Snapshot;
        Assert.ThrowsExactly<InvalidOperationException>(() => session.Step(SessionFixtures.Right));
        Assert.AreSame(before, session.Snapshot);
        session.Step(new(Vector2.Zero, true, false));
        var events = session.DrainEvents();
        CollectionAssert.AreEqual(new long[] { 1, 2 }, events.Select(item => item.Sequence).ToArray());
        Assert.AreEqual(GameEventKind.CoinCollected, events[0].Kind);
        Assert.AreEqual(GameEventKind.Jumped, events[1].Kind);
        Assert.AreEqual(2, session.Snapshot.Tick);
        Assert.AreEqual(2d * SimulationSettings.Default.FixedStepSeconds, session.Snapshot.SimulationSeconds);
    }

    [TestMethod]
    public void RestartResetsIdentityBallBoostTimeCoinsAndEventsWithoutChangingOldValues()
    {
        var session = new GameSession(SessionFixtures.Level(coins: [new("coin", new(0f, 0.501f, 0f), 0.1f)], finishX: 50f),
            SimulationSettings.Default);
        var initial = session.Snapshot;
        session.Step(new(Vector2.Zero, true, true));
        var previous = session.Snapshot;
        var drained = session.DrainEvents();
        session.Step(default);
        session.Restart();
        Assert.AreNotEqual(initial.SessionId, session.Snapshot.SessionId);
        Assert.AreEqual(initial.Ball, session.Snapshot.Ball);
        Assert.AreEqual(0, session.Snapshot.Tick);
        Assert.AreEqual(0d, session.Snapshot.SimulationSeconds);
        Assert.AreEqual(0, session.Snapshot.CoinCount);
        Assert.IsEmpty(session.DrainEvents());
        Assert.AreEqual(1, previous.Tick);
        Assert.HasCount(3, drained);
        session.Step(default);
        Assert.AreEqual(1, session.DrainEvents().Single().Sequence);
    }

    [TestMethod]
    public void InvalidCommandIsAtomicAndCountersRejectOverflow()
    {
        var session = new GameSession(SessionFixtures.Level(), SimulationSettings.Default);
        var initial = session.Snapshot;
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => session.Step(new(new(2f, 0f), false, false)));
        Assert.AreSame(initial, session.Snapshot);
        Assert.IsEmpty(session.DrainEvents());
        Assert.AreEqual(long.MaxValue, GameSession.AdvanceCounter(long.MaxValue - 1, 1));
        Assert.ThrowsExactly<InvalidOperationException>(() => GameSession.AdvanceCounter(long.MaxValue, 1));
    }

    [TestMethod]
    public void RealContactBudgetFailurePreservesLastSnapshotAndUndrainedCoins()
    {
        var level = new LevelDefinition("corner", new(0f, 0.55f, 0f), -10f,
            new OrientedBox(new(20f, 5f, 0f), Vector3.One, Quaternion.Identity),
            [new("floor", new OrientedBox(new(0f, -0.5f, 0f), new(20f, 0.5f, 20f), Quaternion.Identity)),
             new("wall", new OrientedBox(new(1.05f, 0f, 0f), new(0.5f, 10f, 10f), Quaternion.Identity))],
            [new("coin", new(0f, 0.55f, 0f), 0.1f)], []);
        var session = new GameSession(level, new SimulationSettings(moveAcceleration: 100f,
            airControlFactor: 1f, maxContactIterations: 1));
        InvalidOperationException? error = null;
        for (int i = 0; i < 60; i++)
        {
            var before = session.Snapshot;
            var previousPath = session.LastPath;
            try { session.Step(SessionFixtures.Right); }
            catch (InvalidOperationException exception)
            {
                error = exception;
                Assert.AreSame(before, session.Snapshot);
                Assert.AreSame(previousPath, session.LastPath);
                break;
            }
        }
        Assert.IsNotNull(error, "The real two-contact corner must exceed a one-contact budget");
        StringAssert.Contains(error.Message, "MaxContactIterations");
        var coin = session.DrainEvents().Single();
        Assert.AreEqual(GameEventKind.CoinCollected, coin.Kind);
        Assert.AreEqual(1, coin.Sequence);
        Assert.AreEqual(1, session.Snapshot.CoinCount);
    }

    [TestMethod]
    public void HazardMotionExceedingTheConfiguredBudgetIsRejectedWithItsId()
    {
        var level = SessionFixtures.Level();
        var hazard = new HazardDefinition("beam", Vector3.Zero, Vector3.UnitY, Vector3.Zero, Vector3.One, Quaternion.Identity, 10000f, 0f);
        var unsupported = new LevelDefinition(level.Id, level.Spawn, level.KillY, level.Finish, level.Surfaces, [], [hazard]);
        var error = Assert.ThrowsExactly<ArgumentException>(() => new GameSession(unsupported, SimulationSettings.Default));
        StringAssert.Contains(error.Message, "beam");
    }

    [TestMethod]
    public void LastPathMatchesRealPhysicsAndPreviouslyReturnedPathRemainsImmutable()
    {
        var level = SessionFixtures.Level(finishX: 50f);
        var simulator = new BallSimulator(level, SimulationSettings.Default);
        var session = new GameSession(level, SimulationSettings.Default);
        Assert.IsEmpty(session.LastPath);
        var expected = simulator.Step(simulator.CreateInitialState(), SessionFixtures.Right);
        session.Step(SessionFixtures.Right);
        var path = session.LastPath;
        CollectionAssert.AreEqual(expected.Path.ToArray(), path.ToArray());
        Assert.ThrowsExactly<NotSupportedException>(() => ((IList<MotionSegment>)path).Clear());
        var copy = path.ToArray();
        session.DrainEvents();
        Assert.AreSame(path, session.LastPath);
        session.Step(SessionFixtures.Right);
        Assert.AreNotSame(path, session.LastPath);
        CollectionAssert.AreEqual(copy, path.ToArray());
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void FailedStepDoesNotPublishItsPath(bool invalidResult)
    {
        var level = SessionFixtures.Level(finishX: 50f);
        var simulator = new BallSimulator(level, SimulationSettings.Default);
        int calls = 0;
        var session = new GameSession(level, SimulationSettings.Default, (ball, command) =>
        {
            var result = simulator.Step(ball, command);
            if (++calls != 2) return result;
            if (!invalidResult) throw new InvalidOperationException("simulation budget");
            // Физика вернула новый Path, но подготовка snapshot отклоняет результат.
            return new BallStepResult(result.Ball with { Radius = 0f }, result.Path.ToList());
        });
        session.Step(SessionFixtures.Right);
        var before = session.Snapshot;
        var path = session.LastPath;
        if (invalidResult) Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => session.Step(SessionFixtures.Right));
        else Assert.ThrowsExactly<InvalidOperationException>(() => session.Step(SessionFixtures.Right));
        Assert.AreSame(before, session.Snapshot);
        Assert.AreSame(path, session.LastPath);
    }

    [TestMethod]
    public void TerminalStepPreservesPathAndUnreadEventsAndRestartClearsOnlyCurrentPath()
    {
        var session = new GameSession(SessionFixtures.Level(finishX: 0f), SimulationSettings.Default);
        session.Step(default);
        var path = session.LastPath;
        var copy = path.ToArray();
        Assert.IsNotEmpty(path);
        session.Step(default);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => session.Step(new(new(float.NaN, 0f), false, false)));
        Assert.AreSame(path, session.LastPath);
        Assert.AreEqual(GameEventKind.LevelCompleted, session.DrainEvents().Single().Kind);
        session.Restart();
        Assert.IsEmpty(session.LastPath);
        CollectionAssert.AreEqual(copy, path.ToArray());
    }
}
