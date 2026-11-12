using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Platformer.App;
using Platformer.Core;

namespace Platformer.Tests;

[TestClass]
public sealed class ExpertObstacleTests
{
    private static HazardDefinition Motion(HazardMotionKind kind, float speed = 2, float phase = 0, float amplitude = 0.9f) =>
        new("test-motion", new(0, 4, 0), Vector3.UnitX, new(0, -3.5f, 0), new(0.65f),
            Quaternion.Identity, speed, phase, HazardResponse.Knockback, kind, amplitude);

    [TestMethod]
    public void PendulumReturnsThroughBottomAndReversesAtBothExtremes()
    {
        var hazard = Motion(HazardMotionKind.Pendulum);
        var bottom = HazardMotion.Evaluate(hazard, 0).Bounds.Center;
        LevelDefinitionTests.Near(new(0, 0.5f, 0), bottom);
        Vector3 left = HazardMotion.Evaluate(hazard, Math.PI / 4).Bounds.Center;
        Vector3 right = HazardMotion.Evaluate(hazard, 3 * Math.PI / 4).Bounds.Center;
        Assert.AreEqual(left.Y, right.Y, 0.0001f);
        Assert.AreEqual(left.Z, -right.Z, 0.0001f);
        Assert.IsGreaterThan(bottom.Y, left.Y);
        Assert.IsLessThan(0.00001f, HazardMotion.VelocityAt(hazard, Math.PI / 4, left).Length());
        LevelDefinitionTests.Near(bottom, HazardMotion.Evaluate(hazard, Math.PI).Bounds.Center);
        Assert.IsLessThan(0f, HazardMotion.VelocityAt(hazard, 0, bottom).Z * HazardMotion.VelocityAt(hazard, Math.PI / 2, bottom).Z);
        Assert.IsTrue(float.IsFinite(HazardMotion.Evaluate(hazard, double.MaxValue).Bounds.Center.Y));
    }

    [TestMethod]
    [DataRow(HazardMotionKind.Pendulum)] [DataRow(HazardMotionKind.Shuttle)]
    public void MotionDerivativeAndConservativeSpeedBoundCoverEntireCycle(HazardMotionKind kind)
    {
        var hazard = Motion(kind);
        const double dt = 0.0001;
        for (int i = 1; i < 400; i++)
        {
            double time = i * Math.PI / 200;
            var center = HazardMotion.Evaluate(hazard, time).Bounds.Center;
            var measured = (HazardMotion.Evaluate(hazard, time + dt).Bounds.Center
                - HazardMotion.Evaluate(hazard, time - dt).Bounds.Center) / (float)(dt * 2);
            Assert.IsLessThan(0.01f, Vector3.Distance(measured, HazardMotion.VelocityAt(hazard, time, center)));
            Assert.IsLessThanOrEqualTo(HazardMotion.MaximumPointSpeed(hazard) + 0.01, (double)measured.Length());
        }
    }

    [TestMethod]
    public void ShuttleHitsStationaryBallBetweenClearEndpointsAndOverspeedFailsBudget()
    {
        var settings = new SimulationSettings(maxCollisionSubsteps: 128);
        var hazard = new HazardDefinition("fast-piston", Vector3.Zero, Vector3.UnitZ, Vector3.Zero,
            new(0.1f), Quaternion.Identity, MathF.PI / settings.FixedStepSeconds, -MathF.PI / 2,
            HazardResponse.Knockback, HazardMotionKind.Shuttle, 2);
        MotionSegment[] path = [new(Vector3.Zero, Vector3.Zero, 0, 1)];
        foreach (double time in new[] { 0d, settings.FixedStepSeconds })
            Assert.IsGreaterThan(settings.BallRadius, HazardMotion.Evaluate(hazard, time).Bounds.ClosestPoint(Vector3.Zero).Length());
        Assert.IsTrue(HazardCollision.IntersectsPath(hazard, path, 0, settings));
        Assert.ThrowsExactly<InvalidOperationException>(() => HazardCollision.IntersectsPath(hazard, path, 0, SimulationSettings.Default));
    }

    [TestMethod]
    [DataRow(HazardMotionKind.Pendulum)] [DataRow(HazardMotionKind.Shuttle)]
    public void NewObstaclesKnockBackWithoutFailAndRestartRestoresPhase(HazardMotionKind kind)
    {
        var hazard = Motion(kind, speed: 0);
        var floor = new SurfaceDefinition("floor", new(new(0, -0.2f, 0), new(6, 0.2f, 6), Quaternion.Identity));
        var level = new LevelDefinition("contact", new(0.8f, 0.501f, 0), -5,
            new(new(5, 1, 5), new(0.2f), Quaternion.Identity), [floor], [], [hazard]);
        var session = new GameSession(level, SimulationSettings.Default);
        var initial = session.Snapshot.Hazards[0].Bounds;
        session.Step(new(-Vector2.UnitX, false, false));
        Assert.AreEqual(SessionPhase.Running, session.Snapshot.Phase);
        Assert.IsGreaterThan(2f, session.Snapshot.Ball.Velocity.Y);
        Assert.IsGreaterThan(2f, session.Snapshot.Ball.Velocity.X);
        session.Restart();
        Assert.AreEqual(Vector3.Zero, session.Snapshot.Ball.Velocity);
        Assert.AreEqual(initial.Center, session.Snapshot.Hazards[0].Bounds.Center);
        Assert.AreEqual(initial.Orientation, session.Snapshot.Hazards[0].Bounds.Orientation);
    }

    [TestMethod]
    [DataRow(HazardMotionKind.Pendulum)] [DataRow(HazardMotionKind.Shuttle)]
    public void IncomingMovingObstaclePushesAStandingBall(HazardMotionKind kind)
    {
        var hazard = new HazardDefinition("incoming", new(0, kind == HazardMotionKind.Pendulum ? 4 : 0.5f, 0),
            kind == HazardMotionKind.Pendulum ? Vector3.UnitX : Vector3.UnitZ,
            kind == HazardMotionKind.Pendulum ? new(0, -3.5f, 0) : Vector3.Zero, new(0.65f),
            Quaternion.Identity, 1.5f, -MathF.PI / 2, HazardResponse.Knockback, kind,
            kind == HazardMotionKind.Pendulum ? 0.9f : 3);
        var level = new LevelDefinition("standing", new(0, 0.501f, 0), -5,
            new(new(5, 1, 5), new(0.2f), Quaternion.Identity),
            [new("floor", new(new(0, -0.2f, 0), new(6, 0.2f, 6), Quaternion.Identity))], [], [hazard]);
        var session = new GameSession(level, SimulationSettings.Default);
        bool bounced = false;
        for (int i = 0; i < 300 && !bounced; i++)
        {
            session.Step(default);
            Assert.AreEqual(SessionPhase.Running, session.Snapshot.Phase);
            bounced = session.Snapshot.Ball.Velocity.Y > 2;
        }
        Assert.IsTrue(bounced, kind.ToString());
        Assert.IsGreaterThan(2f, MathF.Abs(session.Snapshot.Ball.Velocity.Z));
    }

    [TestMethod]
    public void PendulumStoneClearsItsSolidSupportsThroughoutTheSwing()
    {
        var level = PendulumGorge.Create();
        foreach (var hazard in level.Hazards)
        for (int i = 0; i < 180; i++)
        {
            var stone = HazardMotion.Evaluate(hazard, i / 30d).Bounds;
            foreach (var support in level.Surfaces.Where(s => s.IsBarrier))
                Assert.IsGreaterThan(stone.HalfExtents.Length(),
                    Vector3.Distance(stone.Center, support.Bounds.ClosestPoint(stone.Center)));
        }
    }

    [TestMethod]
    public void ExpertCatalogOrderAndPauseKeepSnapshotAndRopeAlignedWithPhysics()
    {
        var catalog = GameLevels.CreateCatalog();
        CollectionAssert.AreEqual(ChallengeRoutes.ExpertIds, catalog.Levels.Skip(9).Take(3).Select(l => l.Id).ToArray());
        var flow = new LevelFlow(catalog, SimulationSettings.Default, new UserSettings(), visual: true);
        flow.Start("pendulum-gorge");
        for (int i = 0; i < 50; i++) flow.Step(default);
        var before = flow.Snapshot!;
        foreach (var pose in before.Hazards)
            Assert.AreEqual(pose.Bounds.Center, flow.Presenter!.HazardObjects[pose.Id].Transform.Position);
        var rope = flow.Presenter!.Scene.Objects.Single(item => MathF.Abs(item.Transform.Scale.X - 0.065f) < 0.00001f
            && Vector3.Distance(item.Transform.Position, (catalog.Get(before.LevelId).Hazards[0].Pivot + before.Hazards.Single(h => h.Id == "stone-one").Bounds.Center) / 2) < 0.001f);
        Assert.IsGreaterThan(4f, rope.Transform.Scale.Y);
        flow.Pause();
        for (int i = 0; i < 240; i++) flow.Step(default);
        Assert.AreSame(before, flow.Snapshot);
        flow.Continue(); flow.Step(default);
        Assert.AreEqual(before.Tick + 1, flow.Snapshot!.Tick);
    }

    [TestMethod]
    public void InvalidMotionParametersAreRejectedBeforeSessionConstruction()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => Motion((HazardMotionKind)99));
        foreach (float amplitude in new[] { 0f, -1, float.NaN, float.PositiveInfinity, 101f })
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => Motion(HazardMotionKind.Shuttle, amplitude: amplitude));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => Motion(HazardMotionKind.Pendulum, amplitude: 4));
    }

    [TestMethod]
    public void PendulumWithZeroArmDoesNotCreateANanRopeTransform()
    {
        var hazard = new HazardDefinition("zero-arm", new(0, 3, 0), Vector3.UnitX, Vector3.Zero,
            new(0.5f), Quaternion.Identity, 1, 0, HazardResponse.Knockback, HazardMotionKind.Pendulum);
        var level = new LevelDefinition("zero-arm-scene", new(0, 0.501f, 0), -5,
            new(new(5, 1, 5), new(0.2f), Quaternion.Identity),
            [new("floor", new(new(0, -0.2f, 0), new(6, 0.2f, 6), Quaternion.Identity))], [], [hazard]);
        var session = new GameSession(level, SimulationSettings.Default);
        var presenter = new GameScenePresenter(level);
        session.Step(default);
        presenter.Apply(session.Snapshot, GameSkins.All[0]);
        foreach (var item in presenter.Scene.Objects)
        {
            var rotation = item.Transform.Rotation;
            Assert.IsTrue(float.IsFinite(rotation.X) && float.IsFinite(rotation.Y)
                && float.IsFinite(rotation.Z) && float.IsFinite(rotation.W));
            Assert.IsTrue(float.IsFinite(item.Transform.Scale.Y));
        }
    }
}
