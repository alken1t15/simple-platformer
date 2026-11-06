using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Platformer.Core;

namespace Platformer.Tests;

[TestClass]
public sealed class GapCollisionTests
{
    [TestMethod]
    public void TwoGapEdgesSupportRestingBallAndAllowJump()
    {
        var track = TrainingTrack.Create();
        var settings = SimulationSettings.Default;
        float top = 3f * MathF.Tan(MathF.PI / 9f);
        float paddedRadius = settings.BallRadius + settings.SkinWidth;
        Vector3 spawn = new(7f, top + MathF.Sqrt(paddedRadius * paddedRadius - 0.25f), 12.5f);
        var level = new LevelDefinition(track.Id, spawn, track.KillY, track.Finish, track.Surfaces, [], []);
        var simulator = new BallSimulator(level, settings);
        var state = simulator.CreateInitialState();
        Assert.IsTrue(state.Grounded, "Совместная опора на два края должна разрешать выход прыжком.");
        var jumped = simulator.Step(state, new(Vector2.Zero, true, false));
        AssertStep(state, jumped, level);
        Assert.IsGreaterThan(5f, jumped.Ball.Velocity.Y);
        Assert.IsFalse(jumped.Ball.Grounded);
        var airborne = simulator.Step(jumped.Ball, new(Vector2.Zero, true, false));
        Assert.IsLessThan(jumped.Ball.Velocity.Y, airborne.Ball.Velocity.Y, "В воздухе повторного прыжка нет.");
        var landingOnly = new LevelDefinition(track.Id, spawn, track.KillY, track.Finish,
            track.Surfaces.Where(surface => surface.Id == "jump-landing").ToArray(), [], []);
        Assert.IsFalse(new BallSimulator(landingOnly, settings).CreateInitialState().Grounded,
            "Один крутой край не должен становиться допустимым склоном.");
    }

    [TestMethod]
    [DataRow(6.8f, 0f)]
    [DataRow(7f, 0f)]
    [DataRow(7.2f, 0f)]
    [DataRow(7f, 0.7f)]
    [DataRow(7f, -0.7f)]
    public void HoldingForwardIntoTrainingGapDoesNotExhaustContactBudget(float x, float yaw)
    {
        var track = TrainingTrack.Create();
        float top = 3f * MathF.Tan(MathF.PI / 9f);
        Quaternion rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, yaw);
        OrientedBox Rotate(OrientedBox box) => new(Vector3.Transform(box.Center, rotation), box.HalfExtents,
            Quaternion.Normalize(rotation * box.Orientation));
        Vector3 forward = Vector3.Transform(Vector3.UnitZ, rotation), right = Vector3.Transform(Vector3.UnitX, rotation);
        var level = new LevelDefinition(track.Id, Vector3.Transform(new(x, top + 0.501f, 10f), rotation), track.KillY,
            Rotate(track.Finish), track.Surfaces.Select(surface => new SurfaceDefinition(surface.Id, Rotate(surface.Bounds))).ToArray(), [], []);
        var simulator = new BallSimulator(level, SimulationSettings.Default);
        var state = simulator.CreateInitialState();
        for (int tick = 0; tick < 600; tick++)
        {
            var step = simulator.Step(state, new(new(forward.X, forward.Z), false, false));
            AssertStep(state, step, level);
            state = step.Ball;
        }
        // Проверяем выход после настоящего подхода и остановки, не только из spawn между краями.
        var blocked = state;
        for (int tick = 0; tick < 10; tick++) state = simulator.Step(state, default).Ball;
        Assert.IsTrue(state.Grounded);
        var escape = simulator.Step(state, new(Vector2.Zero, true, false));
        AssertStep(state, escape, level);
        Assert.IsGreaterThan(5f, escape.Ball.Velocity.Y);
        state = blocked;
        Vector3 initial = state.Center;
        for (int tick = 0; tick < 90; tick++)
        {
            var step = simulator.Step(state, new(new(right.X, right.Z), false, false));
            AssertStep(state, step, level);
            state = step.Ball;
        }
        Assert.IsGreaterThan(1f, Vector3.Dot(state.Center - initial, right), "Упор не должен блокировать движение вдоль краёв.");
    }

    private static void AssertStep(BallState before, BallStepResult step, LevelDefinition level)
    {
        Assert.IsTrue(float.IsFinite(step.Ball.Center.LengthSquared()));
        Assert.IsTrue(float.IsFinite(step.Ball.Velocity.LengthSquared()));
        Assert.AreEqual(0f, step.Path[0].StartFraction);
        Assert.AreEqual(1f, step.Path[^1].EndFraction);
        Vector3 point = before.Center;
        float fraction = 0f;
        foreach (var segment in step.Path)
        {
            Assert.AreEqual(point, segment.Start);
            Assert.AreEqual(fraction, segment.StartFraction);
            Assert.IsGreaterThan(segment.StartFraction, segment.EndFraction);
            foreach (var surface in level.Surfaces)
                Assert.IsGreaterThanOrEqualTo(-SimulationSettings.Default.PenetrationSlop,
                    SphereGeometry.Contact(surface.Bounds, segment.End, before.Radius).Separation);
            point = segment.End;
            fraction = segment.EndFraction;
        }
        Assert.AreEqual(step.Ball.Center, point);
    }
}
