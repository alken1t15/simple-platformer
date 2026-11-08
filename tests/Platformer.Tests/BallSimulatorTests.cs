using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Platformer.Core;

namespace Platformer.Tests;

[TestClass]
public sealed class BallSimulatorTests
{
    private static readonly GameCommand Right = new(Vector2.UnitX, false, false);
    private static SurfaceDefinition Floor(string id = "floor") =>
        new(id, new OrientedBox(new Vector3(0f, -0.5f, 0f), new Vector3(100f, 0.5f, 100f), Quaternion.Identity));
    private static LevelDefinition Level(Vector3 spawn, params SurfaceDefinition[] surfaces) =>
        new("test", spawn, -10f, new OrientedBox(new Vector3(100f, 10f, 100f), Vector3.One, Quaternion.Identity), surfaces, [], []);
    private static BallState Air(Vector3 center, Vector3 velocity = default) =>
        new(center, velocity, 0.5f, false, Vector3.Zero, 0f, 0f);

    [TestMethod]
    public void FreeTickMatchesIndependentSemiImplicitEulerExample()
    {
        var simulator = new BallSimulator(Level(new Vector3(0f, 5f, 0f)), new SimulationSettings(airControlFactor: 1f));
        var before = simulator.CreateInitialState();
        var step = simulator.Step(before, Right);
        Near(new Vector3(1f / 6f, -0.15f, 0f), step.Ball.Velocity);
        Near(before.Center + new Vector3(1f / 720f, -0.00125f, 0f), step.Ball.Center);
        Assert.IsFalse(step.Ball.Grounded);
        AssertPath(before, step);
    }

    [TestMethod]
    public void AirControlUsesConfiguredFractionAndZeroMeansNoHorizontalForce()
    {
        var level = Level(new Vector3(0f, 5f, 0f));
        var normal = new BallSimulator(level, SimulationSettings.Default);
        Assert.AreEqual(1f / 24f, normal.Step(normal.CreateInitialState(), Right).Ball.Velocity.X, 1e-6f);
        var none = new BallSimulator(level, new SimulationSettings(airControlFactor: 0f));
        var moving = none.CreateInitialState() with { Velocity = new Vector3(2f, 0f, 0f) };
        Assert.AreEqual(2f, none.Step(moving, default).Ball.Velocity.X);
    }

    [TestMethod]
    public void DiagonalAndStraightHaveEqualSpeedAndBrakingStopsOnGround()
    {
        var simulator = new BallSimulator(Level(new Vector3(0f, 0.501f, 0f), Floor()), SimulationSettings.Default);
        var straight = simulator.CreateInitialState();
        var diagonal = straight;
        var diagonalCommand = new GameCommand(Vector2.Normalize(Vector2.One), false, false);
        for (int i = 0; i < 60; i++)
        {
            straight = simulator.Step(straight, Right).Ball;
            diagonal = simulator.Step(diagonal, diagonalCommand).Ball;
        }
        Assert.AreEqual(4.5f, straight.Velocity.Length(), 1e-5f);
        Assert.AreEqual(straight.Velocity.Length(), diagonal.Velocity.Length(), 1e-5f);
        for (int i = 0; i < 60; i++) straight = simulator.Step(straight, default).Ball;
        Near(Vector3.Zero, straight.Velocity);
        Assert.IsTrue(straight.Grounded);
    }

    [TestMethod]
    public void InitialProbeSupportsImmediateJumpAndAirPressDoesNotJumpAgain()
    {
        var simulator = new BallSimulator(Level(new Vector3(0f, 0.501f, 0f), Floor()), SimulationSettings.Default);
        var initial = simulator.CreateInitialState();
        Assert.IsTrue(initial.Grounded);
        Near(Vector3.UnitY, initial.GroundNormal);
        var jump = new GameCommand(Vector2.Zero, true, false);
        var state = simulator.Step(initial, jump).Ball;
        Assert.AreEqual(5.85f, state.Velocity.Y, 1e-5f);
        Assert.IsFalse(state.Grounded);
        state = simulator.Step(state, jump).Ball;
        Assert.AreEqual(5.7f, state.Velocity.Y, 1e-5f);
        float maximum = state.Center.Y;
        for (int i = 2; i < 40; i++)
        {
            state = simulator.Step(state, default).Ball;
            maximum = MathF.Max(maximum, state.Center.Y);
        }
        // v_n = 6 - n*18/120; положительны первые 39 скоростей.
        double discreteHeight = Enumerable.Range(1, 39).Sum(n => (6d - n * 18d / 120d) / 120d);
        Assert.AreEqual(discreteHeight, maximum - initial.Center.Y, 2e-5d);
        for (int i = 0; i < 100; i++) state = simulator.Step(state, default).Ball;
        Assert.IsTrue(state.Grounded);
        Assert.AreEqual(initial.Center.Y, state.Center.Y, 1e-5f);
        Assert.AreEqual(0f, state.Velocity.Y, 1e-5f);
    }

    [TestMethod]
    public void GentleSlopeRollsWithoutInputAndUphillMovementRemainsControlled()
    {
        var (level, normal) = Slope(20f);
        var simulator = new BallSimulator(level, SimulationSettings.Default);
        var initial = simulator.CreateInitialState();
        Assert.IsTrue(initial.Grounded);
        Near(normal, initial.GroundNormal);
        var state = initial;
        for (int i = 0; i < 240; i++) state = simulator.Step(state, default).Ball;
        Assert.IsLessThan(initial.Center.Y - 0.1f, state.Center.Y);
        float uphillStartY = state.Center.Y;
        for (int i = 0; i < 120; i++) state = simulator.Step(state, Right).Ball;
        Assert.IsTrue(state.Grounded);
        Assert.IsGreaterThan(uphillStartY + 0.5f, state.Center.Y);
        Assert.IsLessThanOrEqualTo(4.5f + 1e-5f, state.Velocity.Length());
        Assert.AreEqual(0f, Vector3.Dot(state.Velocity, normal), 1e-5f);
    }

    [TestMethod]
    public void WalkableSlopesWithDifferentWorldOrientationsKeepSupport()
    {
        foreach (float angle in new[] { 5f, 20f, 30f, 45f, 49.9f })
        foreach (float yaw in new[] { 0f, 0.4f, 1.2f, 2.7f })
        {
            var tilt = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, angle * MathF.PI / 180f);
            var rotation = Quaternion.Concatenate(tilt, Quaternion.CreateFromAxisAngle(Vector3.UnitY, yaw));
            var box = new OrientedBox(Vector3.Zero, new Vector3(20f, 0.2f, 20f), rotation);
            Vector3 normal = Vector3.Transform(Vector3.UnitY, box.Orientation);
            Vector3 spawn = box.ToWorld(Vector3.UnitY * 0.2f) + normal * 0.501f;
            var simulator = new BallSimulator(Level(spawn, new SurfaceDefinition("slope", box)), SimulationSettings.Default);
            var state = simulator.CreateInitialState();
            for (int tick = 0; tick < 120; tick++)
            {
                var move = new GameCommand(Vector2.Normalize(new Vector2(1f, 0.3f)), false, false);
                state = simulator.Step(state, move).Ball;
                Assert.IsTrue(state.Grounded, $"angle {angle}, yaw {yaw}, tick {tick}");
            }
            Assert.IsLessThanOrEqualTo(4.5f + 1e-5f, state.Velocity.Length());
        }
    }

    [TestMethod]
    public void SixtyDegreeSlopeIsNotGroundAndDoesNotAllowJump()
    {
        var (level, _) = Slope(60f);
        var simulator = new BallSimulator(level, SimulationSettings.Default);
        var initial = simulator.CreateInitialState();
        Assert.IsFalse(initial.Grounded);
        var state = simulator.Step(initial, new GameCommand(Vector2.Zero, true, false)).Ball;
        Assert.IsFalse(state.Grounded);
        Assert.IsLessThanOrEqualTo(0f, state.Velocity.Y);
        for (int i = 0; i < 60; i++) state = simulator.Step(state, default).Ball;
        Assert.IsLessThan(initial.Center.Y - 0.1f, state.Center.Y);
    }

    [TestMethod]
    public void CeilingRemovesUpwardVelocityWithoutBecomingGround()
    {
        var ceiling = new SurfaceDefinition("ceiling", new OrientedBox(new Vector3(0f, 2f, 0f),
            new Vector3(10f, 0.1f, 10f), Quaternion.Identity));
        var simulator = new BallSimulator(Level(Vector3.UnitY, ceiling), SimulationSettings.Default);
        var state = Air(Vector3.UnitY, Vector3.UnitY * 10f);
        bool stopped = false;
        for (int i = 0; i < 15; i++)
        {
            state = simulator.Step(state, default).Ball;
            Assert.IsLessThanOrEqualTo(1.399f + 1e-5f, state.Center.Y);
            Assert.IsFalse(state.Grounded);
            stopped |= state.Velocity.Y == 0f;
        }
        Assert.IsTrue(stopped);
    }

    [TestMethod]
    public void ShallowSpawnPenetrationProjectsButDeepAndInteriorSpawnAreRejected()
    {
        var simulator = new BallSimulator(Level(new Vector3(0f, 0.4995f, 0f), Floor()), SimulationSettings.Default);
        var state = simulator.CreateInitialState();
        Assert.AreEqual(0.501f, state.Center.Y, 1e-6f);
        Assert.IsTrue(state.Grounded);
        Near(Vector3.Zero, state.Velocity);
        Assert.AreEqual(0f, state.BoostRemainingSeconds);
        Assert.AreEqual(0f, state.BoostCooldownSeconds);
        foreach (float y in new[] { 0.49f, -0.5f })
        {
            var bad = new BallSimulator(Level(new Vector3(0f, y, 0f), Floor("spawn-floor")), SimulationSettings.Default);
            var error = Assert.ThrowsExactly<ArgumentException>(() => bad.CreateInitialState());
            StringAssert.Contains(error.Message, "spawn-floor");
        }
    }

    [TestMethod]
    public void BoostLasts120TicksThenCooldown360TicksAndCannotRestartEarly()
    {
        var simulator = new BallSimulator(Level(new Vector3(0f, 0.501f, 0f), Floor()), SimulationSettings.Default);
        var initial = simulator.CreateInitialState();
        var state = initial with { Velocity = new Vector3(4.5f, 0f, 0f) };
        var pressed = new GameCommand(Vector2.UnitX, false, true);
        state = simulator.Step(state, pressed).Ball;
        Assert.IsGreaterThan(4.5f, state.Velocity.X); // Усиленная цель уже в tick нажатия.
        for (int tick = 2; tick <= 120; tick++)
        {
            state = simulator.Step(state, pressed).Ball;
            if (tick < 120) Assert.IsGreaterThan(0f, state.BoostRemainingSeconds, $"tick {tick}");
        }
        Assert.AreEqual(0f, state.BoostRemainingSeconds);
        Assert.AreEqual(3f, state.BoostCooldownSeconds, 1e-6f);
        Assert.AreEqual(8.1f, state.Velocity.X, 1e-5f);
        for (int tick = 1; tick <= 360; tick++)
        {
            state = simulator.Step(state, pressed).Ball;
            Assert.AreEqual(0f, state.BoostRemainingSeconds);
            if (tick < 360) Assert.IsGreaterThan(0f, state.BoostCooldownSeconds, $"cooldown tick {tick}");
        }
        Assert.AreEqual(0f, state.BoostCooldownSeconds);
        Assert.IsGreaterThan(0f, simulator.Step(state, pressed).Ball.BoostRemainingSeconds);
    }

    [TestMethod]
    public void PartialFinalBoostTickTransfersExcessTimeToCooldown()
    {
        var settings = new SimulationSettings(boostDurationSeconds: 0.01f);
        var simulator = new BallSimulator(Level(new Vector3(0f, 5f, 0f)), settings);
        var state = simulator.Step(simulator.CreateInitialState(), new GameCommand(Vector2.Zero, false, true)).Ball;
        state = simulator.Step(state, default).Ball;
        Assert.AreEqual(0f, state.BoostRemainingSeconds);
        Assert.AreEqual(3f - (2f * settings.FixedStepSeconds - 0.01f), state.BoostCooldownSeconds, 1e-6f);
    }

    [TestMethod]
    public void LongTimersAtSmallestStepDoNotAccumulateAnExtraTick()
    {
        var settings = new SimulationSettings(fixedStepSeconds: 1f / 240f, boostDurationSeconds: 5f, boostCooldownSeconds: 10f);
        var simulator = new BallSimulator(Level(new Vector3(0f, 0.501f, 0f), Floor()), settings);
        var state = simulator.Step(simulator.CreateInitialState(), new GameCommand(Vector2.Zero, false, true)).Ball;
        for (int i = 1; i < 1200; i++) state = simulator.Step(state, default).Ball;
        Assert.AreEqual(0f, state.BoostRemainingSeconds);
        Assert.AreEqual(10f, state.BoostCooldownSeconds, 1e-6f);
        for (int i = 0; i < 2400; i++) state = simulator.Step(state, default).Ball;
        Assert.AreEqual(0f, state.BoostCooldownSeconds);
    }

    [TestMethod]
    public void MaximumConfiguredBoostCannotTunnelThroughThinWall()
    {
        var wall = new SurfaceDefinition("thin-wall", new OrientedBox(new Vector3(0.5f, 1f, 0f),
            new Vector3(0.001f, 10f, 10f), Quaternion.Identity));
        var settings = new SimulationSettings(fixedStepSeconds: 1f / 30f, ballRadius: 0.1f,
            moveSpeed: 20f, airControlFactor: 0f, boostMultiplier: 3f, maxCollisionSubsteps: 128);
        var simulator = new BallSimulator(Level(Vector3.UnitY, wall), settings);
        var before = simulator.CreateInitialState() with { Velocity = new Vector3(60f, 0f, 0f) };
        var step = simulator.Step(before, new GameCommand(Vector2.UnitX, false, true));
        Assert.AreEqual(0.398f, step.Ball.Center.X, 1e-5f);
        Assert.AreEqual(0f, step.Ball.Velocity.X, 1e-5f);
        AssertPath(before, step);
        foreach (var segment in step.Path)
            Assert.IsLessThanOrEqualTo(0.025f + 1e-5f, Vector3.Distance(segment.Start, segment.End));
    }

    [TestMethod]
    public void PathFollowsCollisionAndSlideAndCoversStationaryTime()
    {
        var simulator = CornerSimulator();
        var before = Air(new Vector3(0f, 0.55f, 0f), new Vector3(8f, -8f, 8f));
        var step = simulator.Step(before, default);
        AssertPath(before, step);
        Assert.IsGreaterThanOrEqualTo(2, step.Path.Count);
        Assert.IsLessThanOrEqualTo(0.049f + 1e-5f, step.Ball.Center.X);
        Assert.IsGreaterThanOrEqualTo(0.501f - 1e-5f, step.Ball.Center.Y);
        Assert.IsGreaterThan(0.06f, step.Ball.Center.Z);
        foreach (var segment in step.Path)
        {
            Vector3 midpoint = (segment.Start + segment.End) / 2f;
            Assert.IsLessThanOrEqualTo(0.049f + 1e-5f, midpoint.X);
            Assert.IsGreaterThanOrEqualTo(0.501f - 1e-5f, midpoint.Y);
        }
        var stationary = new BallSimulator(Level(new Vector3(0f, 0.501f, 0f), Floor()), SimulationSettings.Default);
        var initial = stationary.CreateInitialState();
        var still = stationary.Step(initial, default);
        AssertPath(initial, still);
        Assert.HasCount(1, still.Path);
        Assert.AreEqual(still.Path[0].Start, still.Path[0].End);
        Assert.ThrowsExactly<NotSupportedException>(() => ((IList<MotionSegment>)still.Path).Clear());
    }

    [TestMethod]
    public void BudgetFailureDoesNotChangeInputOrSimulatorAndReportsSurface()
    {
        var simulator = CornerSimulator(1);
        var before = Air(new Vector3(0f, 0.55f, 0f), new Vector3(8f, -8f, 0f));
        var saved = before;
        var failure = Assert.ThrowsExactly<InvalidOperationException>(() => simulator.Step(before, default));
        StringAssert.Contains(failure.Message, "wall");
        StringAssert.Contains(failure.Message, "MaxContactIterations");
        Assert.AreEqual(saved, before);
        var clear = Air(new Vector3(-5f, 5f, 0f));
        Assert.AreEqual(simulator.Step(clear, default).Ball, simulator.Step(clear, default).Ball);
        var extreme = clear with { Velocity = new Vector3(10000f, 0f, 0f) };
        StringAssert.Contains(Assert.ThrowsExactly<InvalidOperationException>(() => simulator.Step(extreme, default)).Message,
            "MaxCollisionSubsteps");
    }

    [TestMethod]
    public void SurfaceOrderDoesNotChangeSimultaneousCollisionResult()
    {
        var floor = Floor();
        var wall = new SurfaceDefinition("wall", new OrientedBox(new Vector3(1.05f, 0f, 0f), new Vector3(0.5f, 10f, 10f), Quaternion.Identity));
        var settings = new SimulationSettings(airControlFactor: 0f, gravity: 0.0001f);
        var first = new BallSimulator(Level(new Vector3(0f, 0.55f, 0f), floor, wall), settings);
        var second = new BallSimulator(Level(new Vector3(0f, 0.55f, 0f), wall, floor), settings);
        var before = Air(new Vector3(0f, 0.55f, 0f), new Vector3(8f, -8f, 2f));
        var a = first.Step(before, default);
        var b = second.Step(before, default);
        Assert.AreEqual(a.Ball, b.Ball);
        CollectionAssert.AreEqual(a.Path.ToArray(), b.Path.ToArray());
    }

    [TestMethod]
    public void TrainingTrackRampAndBridgeUseDefaultSettingsAndLeavingBridgeFalls()
    {
        var level = TrainingTrack.Create();
        var simulator = new BallSimulator(level, SimulationSettings.Default);
        var state = simulator.CreateInitialState();
        for (int i = 0; i < 360 && state.Center.X < 6f; i++) state = simulator.Step(state, Right).Ball;
        for (int i = 0; i < 60; i++) state = simulator.Step(state, default).Ball;
        float top = 3f * MathF.Tan(MathF.PI / 9f);
        Assert.IsGreaterThanOrEqualTo(6f, state.Center.X);
        Assert.AreEqual(top + 0.501f, state.Center.Y, 2e-4f);
        Assert.IsTrue(state.Grounded);

        state = Air(new Vector3(7f, top + 0.501f, 3f));
        var forward = new GameCommand(Vector2.UnitY, false, false);
        for (int i = 0; i < 160; i++) state = simulator.Step(state, forward).Ball;
        Assert.IsTrue(state.Grounded);
        Assert.IsTrue(state.Center.Z > 8f && state.Center.Z < 12f);

        state = Air(new Vector3(7f, top + 0.501f, 5f));
        for (int i = 0; i < 120; i++) state = simulator.Step(state, Right).Ball;
        Assert.IsFalse(state.Grounded);
        Assert.IsLessThan(0f, state.Center.Y);
    }

    [TestMethod]
    public void OrdinaryJumpCrossesTrainingTrackGapWithoutBoost()
    {
        var simulator = new BallSimulator(TrainingTrack.Create(), SimulationSettings.Default);
        float top = 3f * MathF.Tan(MathF.PI / 9f);
        var before = Air(new Vector3(7f, top + 0.501f, 11f), new Vector3(0f, 0f, 4.5f));
        var state = simulator.Step(before, new GameCommand(Vector2.UnitY, true, false)).Ball;
        Assert.IsFalse(state.Grounded);
        for (int i = 0; i < 120 && !state.Grounded; i++)
            state = simulator.Step(state, new GameCommand(Vector2.UnitY, false, false)).Ball;
        Assert.IsTrue(state.Grounded);
        Assert.IsTrue(state.Center.Z >= 13.5f && state.Center.Z <= 17f);
        Assert.AreEqual(top + 0.501f, state.Center.Y, 2e-4f);
        Assert.AreEqual(0f, state.BoostRemainingSeconds);
    }

    [TestMethod]
    public void InvalidCommandStateAndNullArgumentsAreRejectedBeforeWork()
    {
        var simulator = new BallSimulator(Level(Vector3.UnitY), SimulationSettings.Default);
        var before = simulator.CreateInitialState();
        foreach (var vector in new[] { new Vector2(float.NaN, 0f), new Vector2(float.PositiveInfinity), Vector2.One })
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => simulator.Step(before, new GameCommand(vector, false, false)));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => simulator.Step(before with { Radius = 0.6f }, default));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => simulator.Step(before with { Center = new Vector3(float.NaN) }, default));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => simulator.Step(before with { Velocity = new Vector3(float.PositiveInfinity) }, default));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => simulator.Step(before with { BoostRemainingSeconds = -1f }, default));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => simulator.Step(before with { BoostCooldownSeconds = float.NaN }, default));
        Assert.ThrowsExactly<ArgumentException>(() => simulator.Step(before with { BoostRemainingSeconds = 0.5f, BoostCooldownSeconds = 1f }, default));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => simulator.Step(before with { Grounded = true, GroundNormal = Vector3.Zero }, default));
        Assert.ThrowsExactly<ArgumentNullException>(() => new BallSimulator(null!, SimulationSettings.Default));
        Assert.ThrowsExactly<ArgumentNullException>(() => new BallSimulator(Level(Vector3.Zero), null!));
        var floor = new BallSimulator(Level(Vector3.UnitY, Floor()), SimulationSettings.Default);
        StringAssert.Contains(Assert.ThrowsExactly<ArgumentException>(() => floor.Step(Air(Vector3.Zero), default)).Message, "floor");
    }

    [TestMethod]
    public void FallingSpeedIsCappedAndEarlierResultRemainsUnchanged()
    {
        var simulator = new BallSimulator(Level(new Vector3(0f, 50f, 0f)), SimulationSettings.Default);
        var before = simulator.CreateInitialState() with { Velocity = new Vector3(0f, -30f, 0f) };
        var first = simulator.Step(before, default);
        var savedPath = first.Path.ToArray();
        var second = simulator.Step(first.Ball, Right);
        Assert.AreEqual(-30f, first.Ball.Velocity.Y);
        Assert.AreEqual(49.75f, first.Ball.Center.Y, 1e-5f);
        AssertPath(before, first);
        AssertPath(first.Ball, second);
        CollectionAssert.AreEqual(savedPath, first.Path.ToArray());
    }

    [TestMethod]
    public void SupportedRadiusAndStepExtremesLandOnThinBridge()
    {
        var bridge = new SurfaceDefinition("thin-bridge", new OrientedBox(Vector3.Zero,
            new Vector3(0.7f, 0.001f, 3f), Quaternion.Identity));
        foreach (float radius in new[] { 0.1f, 0.5f, 2f })
        foreach (float dt in new[] { 1f / 240f, 1f / 120f, 1f / 30f })
        {
            var settings = new SimulationSettings(fixedStepSeconds: dt, ballRadius: radius, maxCollisionSubsteps: 128);
            var simulator = new BallSimulator(Level(new Vector3(0f, radius + 0.5f, 0f), bridge), settings);
            var state = simulator.CreateInitialState() with { Velocity = -Vector3.UnitY * settings.MaxFallSpeed };
            for (int i = 0; i < 120 && !state.Grounded; i++) state = simulator.Step(state, default).Ball;
            Assert.IsTrue(state.Grounded, $"radius {radius}, dt {dt}");
            Assert.AreEqual(0.001f + radius + settings.SkinWidth, state.Center.Y, 1e-5f);
        }
    }

    [TestMethod]
    public void ThinSkinAtLargeWorldCoordinatesDoesNotRejectValidLanding()
    {
        foreach (float height in new[] { -9000f, 9000f })
        {
            var floor = new SurfaceDefinition("distant-floor", new OrientedBox(new Vector3(0f, height - 0.5f, 0f),
                new Vector3(10f, 0.5f, 10f), Quaternion.Identity));
            var settings = new SimulationSettings(ballRadius: 0.3f, skinWidth: 0.0001f);
            var simulator = new BallSimulator(Level(new Vector3(0f, height + 0.8f, 0f), floor), settings);
            var state = simulator.CreateInitialState();
            for (int tick = 0; tick < 120 && !state.Grounded; tick++) state = simulator.Step(state, default).Ball;
            Assert.IsTrue(state.Grounded);
            Assert.IsGreaterThanOrEqualTo(-settings.PenetrationSlop,
                SphereGeometry.Contact(floor.Bounds, state.Center, settings.BallRadius).Separation);
            for (int tick = 0; tick < 10; tick++) state = simulator.Step(state, default).Ball;
            Assert.IsTrue(state.Grounded);

            var shallowSettings = new SimulationSettings(ballRadius: 0.50005f, skinWidth: 0.0001f);
            var shallow = new BallSimulator(Level(new Vector3(0f, height + 0.5f, 0f), floor), shallowSettings);
            var projected = shallow.CreateInitialState();
            Assert.IsTrue(projected.Grounded);
            Assert.IsGreaterThanOrEqualTo(0f,
                SphereGeometry.Contact(floor.Bounds, projected.Center, shallowSettings.BallRadius).Separation);
            Assert.IsTrue(shallow.Step(projected, default).Ball.Grounded);
        }
    }

    [TestMethod]
    public void CommandRoundingIsNormalizedAndBoostWithNoCooldownCanBeUsedAgain()
    {
        var settings = new SimulationSettings(boostDurationSeconds: 1f / 240f, boostCooldownSeconds: 0f);
        var simulator = new BallSimulator(Level(new Vector3(0f, 0.501f, 0f), Floor()), settings);
        var before = simulator.CreateInitialState();
        var rounded = new GameCommand(new Vector2(1f + 5e-7f, 0f), false, true);
        var first = simulator.Step(before, rounded);
        var exact = simulator.Step(before, Right with { BoostPressed = true });
        Assert.AreEqual(exact.Ball, first.Ball);
        Assert.AreEqual(0f, first.Ball.BoostRemainingSeconds);
        Assert.AreEqual(0f, first.Ball.BoostCooldownSeconds);
        var second = simulator.Step(first.Ball, rounded);
        Assert.IsGreaterThan(first.Ball.Velocity.X, second.Ball.Velocity.X);
    }

    private static (LevelDefinition Level, Vector3 Normal) Slope(float degrees)
    {
        var rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, degrees * MathF.PI / 180f);
        var box = new OrientedBox(Vector3.Zero, new Vector3(20f, 0.2f, 10f), rotation);
        Vector3 normal = Vector3.Transform(Vector3.UnitY, box.Orientation);
        Vector3 spawn = box.ToWorld(new Vector3(0f, 0.2f, 0f)) + normal * 0.501f;
        return (Level(spawn, new SurfaceDefinition("slope", box)), normal);
    }

    private static BallSimulator CornerSimulator(int iterations = 8) =>
        new(Level(new Vector3(0f, 0.55f, 0f), Floor(),
            new SurfaceDefinition("wall", new OrientedBox(new Vector3(1.05f, 0f, 0f), new Vector3(0.5f, 10f, 10f), Quaternion.Identity))),
            new SimulationSettings(airControlFactor: 0f, gravity: 0.0001f, maxContactIterations: iterations));

    private static void AssertPath(BallState before, BallStepResult step)
    {
        Assert.IsNotEmpty(step.Path);
        Assert.AreEqual(0f, step.Path[0].StartFraction);
        Assert.AreEqual(1f, step.Path[^1].EndFraction);
        Near(before.Center, step.Path[0].Start);
        Near(step.Ball.Center, step.Path[^1].End);
        for (int i = 0; i < step.Path.Count; i++)
        {
            var segment = step.Path[i];
            Assert.IsGreaterThan(segment.StartFraction, segment.EndFraction);
            if (i == 0) continue;
            Assert.AreEqual(step.Path[i - 1].EndFraction, segment.StartFraction);
            Near(step.Path[i - 1].End, segment.Start);
        }
    }

    private static void Near(Vector3 expected, Vector3 actual) => LevelDefinitionTests.Near(expected, actual);
}
