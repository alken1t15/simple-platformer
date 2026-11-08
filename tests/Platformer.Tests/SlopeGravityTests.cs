using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Platformer.Core;

namespace Platformer.Tests;

[TestClass]
public sealed class SlopeGravityTests
{
    [TestMethod]
    public void FlatGroundKeepsGradualBoostBrakingAndEventuallyStops()
    {
        var settings = SimulationSettings.Default;
        var simulator = new BallSimulator(SessionFixtures.Level(finishX: 100), settings);
        var initial = simulator.CreateInitialState();
        float boostedSpeed = settings.MoveSpeed * settings.BoostMultiplier;
        var state = initial with
        {
            Velocity = Vector3.UnitX * boostedSpeed,
            BoostRemainingSeconds = settings.FixedStepSeconds,
        };
        state = simulator.Step(state, new(Vector2.UnitX, false, false)).Ball;
        state = simulator.Step(state, new(Vector2.UnitX, false, false)).Ball;
        Assert.IsGreaterThan(settings.MoveSpeed, state.Velocity.X);
        Assert.IsLessThan(boostedSpeed, state.Velocity.X);
        for (int i = 0; i < 120; i++) state = simulator.Step(state, default).Ball;
        Assert.AreEqual(Vector3.Zero, state.Velocity);
        var resting = state;
        for (int i = 0; i < 120; i++) state = simulator.Step(state, default).Ball;
        Assert.AreEqual(resting.Center, state.Center);
    }

    [TestMethod]
    public void ReleasedBallRollsDownhillAcrossSlopeAnglesAndWorldDirections()
    {
        foreach (float degrees in new[] { 5f, 20f, 45f })
        foreach (float yaw in new[] { 0f, 0.7f, 2.3f })
        {
            var rotation = Quaternion.Concatenate(
                Quaternion.CreateFromAxisAngle(Vector3.UnitZ, degrees * MathF.PI / 180),
                Quaternion.CreateFromAxisAngle(Vector3.UnitY, yaw));
            var box = new OrientedBox(Vector3.Zero, new(30, 0.2f, 30), rotation);
            var normal = Vector3.Transform(Vector3.UnitY, rotation);
            var level = new LevelDefinition("slope", box.ToWorld(new(0, 0.2f, 0)) + normal * 0.501f,
                -50, new(new(100, 100, 100), Vector3.One, Quaternion.Identity), [new("ramp", box)], [], []);
            var simulator = new BallSimulator(level, SimulationSettings.Default);
            var initial = simulator.CreateInitialState();
            var state = initial;
            for (int tick = 0; tick < 120; tick++)
            {
                state = simulator.Step(state, default).Ball;
                Assert.IsTrue(state.Grounded, $"{degrees}°, yaw {yaw}, tick {tick}");
                Assert.AreEqual(0, Vector3.Dot(state.Velocity, normal), 1e-4f);
            }
            Assert.IsLessThan(initial.Center.Y - 0.01f, state.Center.Y);
            Assert.IsLessThan(0f, state.Velocity.Y);
            Assert.IsLessThanOrEqualTo(SimulationSettings.Default.MoveSpeed + 1e-4f, state.Velocity.Length());
            var horizontal = new Vector3(state.Center.X - initial.Center.X, 0, state.Center.Z - initial.Center.Z);
            Assert.IsGreaterThan(0f, Vector3.Dot(horizontal, new(normal.X, 0, normal.Z)));
        }
    }

    [TestMethod]
    public void ReleasedUphillBallTurnsAroundAndCanJumpFromTheSlope()
    {
        float angle = 20 * MathF.PI / 180;
        var rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, angle);
        var box = new OrientedBox(Vector3.Zero, new(30, 0.2f, 30), rotation);
        var normal = Vector3.Transform(Vector3.UnitY, rotation);
        var level = new LevelDefinition("slope", box.ToWorld(new(0, 0.2f, 0)) + normal * 0.501f,
            -50, new(new(100, 100, 100), Vector3.One, Quaternion.Identity), [new("ramp", box)], [], []);
        var simulator = new BallSimulator(level, SimulationSettings.Default);
        var state = simulator.CreateInitialState();
        for (int i = 0; i < 120; i++) state = simulator.Step(state, new(Vector2.UnitX, false, false)).Ball;
        Assert.IsGreaterThan(0f, state.Velocity.Y);
        for (int i = 0; i < 180; i++) state = simulator.Step(state, default).Ball;
        Assert.IsLessThan(0f, state.Velocity.Y);
        Assert.IsTrue(state.Grounded);
        state = simulator.Step(state, new(Vector2.Zero, true, false)).Ball;
        Assert.IsGreaterThan(0f, state.Velocity.Y);
        Assert.IsFalse(state.Grounded);
    }
}
