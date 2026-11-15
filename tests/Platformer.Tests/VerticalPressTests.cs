using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Platformer.Core;
namespace Platformer.Tests;
[TestClass]
public sealed class VerticalPressTests
{
    private static LevelDefinition Level(Vector3 spawn, float phase) => new("press-test", spawn, -5,
        new(new(20, 1, 0), new(1), Quaternion.Identity),
        [new("floor", new(new(0, -0.5f, 0), new(30, 0.5f, 4), Quaternion.Identity))], [],
        [new("press", new(0, 2.5f, 0), Vector3.UnitY, Vector3.Zero, new(0.6f, 0.65f, 1.8f),
            Quaternion.Identity, 1.35f, phase, HazardResponse.Fail, HazardMotionKind.Shuttle, 2)]);
    [TestMethod]
    public void LoweredPressBlocksSideWithoutDeathOrCollectingCancelledPath()
    {
        Vector3 start = new(-1.2f, 0.501f, 0);
        var session = new GameSession(Level(start, -MathF.PI / 2), SimulationSettings.Default,
            (ball, _) => new(ball with { Center = new(-0.9f, ball.Center.Y, 0), Velocity = Vector3.UnitX },
                [new(ball.Center, new(-0.9f, ball.Center.Y, 0), 0, 1)]));
        session.Step(default);
        Assert.AreEqual(SessionPhase.Running, session.Snapshot.Phase);
        Assert.AreEqual(start, session.Snapshot.Ball.Center);
        Assert.AreEqual(0f, session.Snapshot.Ball.Velocity.X);
    }
    [TestMethod]
    public void DescendingPressKillsBallUnderneath()
    {
        var session = new GameSession(Level(new(0, 0.501f, 0), MathF.PI), SimulationSettings.Default);
        for (int i = 0; i < 180 && session.Snapshot.Phase == SessionPhase.Running; i++) session.Step(default);
        Assert.AreEqual(SessionPhase.Failed, session.Snapshot.Phase);
    }
    [TestMethod]
    public void GrazingShuttleContactDoesNotExhaustCollisionBudget()
    {
        var settings = SimulationSettings.Default;
        var press = new HazardDefinition("grazing", new(0, 0.5f, 0), Vector3.UnitY, Vector3.Zero,
            new(0.6f, 0.65f, 1.8f), Quaternion.Identity, 0, 0, HazardResponse.Fail, HazardMotionKind.Shuttle, 2);
        float x = 0.6f + settings.BallRadius + settings.PositionEpsilon * 0.25f;
        MotionSegment[] path = [new(new(x, 0.5f, -0.02f), new(x, 0.5f, 0.02f), 0, 1)];
        for (int i = 0; i < 1000; i++)
            Assert.IsTrue(HazardCollision.IntersectsPath(press, path, i * settings.FixedStepSeconds, settings));
        MotionSegment[] clear = [new(new(x + 0.1f, 0.5f, 0), new(x + 0.1f, 0.5f, 0.02f), 0, 1)];
        Assert.IsFalse(HazardCollision.IntersectsPath(press, clear, 0, settings));
    }}