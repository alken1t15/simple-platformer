using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Platformer.Core;

namespace Platformer.Tests;

[TestClass]
public sealed class JumpCircuitTests
{
    [TestMethod]
    public void WalkingIntoAStationaryWallFailsOnceAndRestartRestoresTheEntireLevel()
    {
        var level = JumpCircuit.Create();
        var session = new GameSession(level, SimulationSettings.Default);
        var before = session.Snapshot;
        for (int i = 0; i < 400 && session.Snapshot.Phase == SessionPhase.Running; i++)
            session.Step(new(Vector2.UnitX, false, false));
        Assert.AreEqual(SessionPhase.Failed, session.Snapshot.Phase);
        var events = session.DrainEvents();
        Assert.AreEqual(1, events.Count(e => e.Kind == GameEventKind.Failed));
        Assert.IsLessThan(5.5f, session.Snapshot.Ball.Center.X);
        var failed = session.Snapshot;
        session.Step(default);
        Assert.AreSame(failed, session.Snapshot);
        Assert.IsEmpty(session.DrainEvents());
        session.Restart();
        Assert.AreNotEqual(before.SessionId, session.Snapshot.SessionId);
        Assert.AreEqual(before.Ball, session.Snapshot.Ball);
        Assert.AreEqual(0, session.Snapshot.CoinCount);
        Assert.AreEqual(0, session.Snapshot.SimulationSeconds);
        for (int i = 0; i < before.Hazards.Count; i++)
        {
            Assert.AreEqual(before.Hazards[i].Bounds.Center, session.Snapshot.Hazards[i].Bounds.Center);
            Assert.AreEqual(before.Hazards[i].Bounds.Orientation, session.Snapshot.Hazards[i].Bounds.Orientation);
        }
    }
}
