using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Platformer.App;
using Platformer.Core;

namespace Platformer.Tests;

[TestClass]
public sealed class HazardTests
{
    [TestMethod]
    public void PreviewUsesTheSimulatedPoseAndRestartRestoresPhase()
    {
        var preview = new PresenterPreview(overview: true);
        var beam = preview.Presenter.HazardObjects["preview-beam"];
        var initial = beam.Transform.Rotation;
        for (int i = 0; i < 60; i++) preview.Advance();
        Assert.AreNotEqual(initial, beam.Transform.Rotation);
        preview.Restart();
        Assert.AreEqual(initial, beam.Transform.Rotation);
    }
    private static HazardDefinition Beam(float speed = 0f, float angle = 0f, Vector3? offset = null) =>
        new("beam", Vector3.Zero, Vector3.UnitY, offset ?? Vector3.Zero,
            new Vector3(2f, 0.15f, 0.15f), Quaternion.Identity, speed, angle);

    [TestMethod]
    public void PoseRotatesCenterAndOrientationAroundWorldAxis()
    {
        var beam = Beam(1f, 0f, Vector3.UnitX);
        var pose = HazardMotion.Evaluate(beam, Math.PI / 2d);
        LevelDefinitionTests.Near(new Vector3(0f, 0f, -1f), pose.Bounds.Center);
        LevelDefinitionTests.Near(-Vector3.UnitZ, pose.Bounds.ToWorld(Vector3.UnitX) - pose.Bounds.Center);
        Assert.AreEqual("beam", pose.Id);
        LevelDefinitionTests.Near(Vector3.UnitX, HazardMotion.Evaluate(beam, 0d).Bounds.Center);
    }

    [TestMethod]
    public void StationaryBallHitBetweenBothNonOverlappingEndpoints()
    {
        var settings = SimulationSettings.Default;
        float speed = (MathF.PI / 2f) / settings.FixedStepSeconds;
        var beam = Beam(speed, -MathF.PI / 4f);
        Vector3 point = new(1.8f, 0f, 0f);
        MotionSegment[] path = [new(point, point, 0f, 1f)];
        Assert.IsGreaterThan(settings.BallRadius,
            Vector3.Distance(point, HazardMotion.Evaluate(beam, 0).Bounds.ClosestPoint(point)));
        Assert.IsGreaterThan(settings.BallRadius,
            Vector3.Distance(point, HazardMotion.Evaluate(beam, settings.FixedStepSeconds).Bounds.ClosestPoint(point)));
        Assert.IsTrue(HazardCollision.IntersectsPath(beam, path, 0d, settings));
    }

    [TestMethod]
    public void MovingBallCrossesStaticBeamAndDistantBallDoesNot()
    {
        var settings = SimulationSettings.Default;
        Assert.IsTrue(HazardCollision.IntersectsPath(Beam(),
            [new(new Vector3(0, 0, -2), new Vector3(0, 0, 2), 0, 1)], 0, settings));
        Assert.IsFalse(HazardCollision.IntersectsPath(Beam(1f),
            [new(new Vector3(0, 8, -1), new Vector3(0, 8, 1), 0, 1)], 0, settings));
    }

    [TestMethod]
    public void RejectsBrokenPathAndInvalidTime()
    {
        var settings = SimulationSettings.Default;
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => HazardMotion.Evaluate(Beam(), double.NaN));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => HazardCollision.IntersectsPath(Beam(),
            [new(Vector3.Zero, Vector3.Zero, 0, 1)], -1, settings));
        Assert.ThrowsExactly<ArgumentException>(() => HazardCollision.IntersectsPath(Beam(), [], 0, settings));
        Assert.ThrowsExactly<ArgumentException>(() => HazardCollision.IntersectsPath(Beam(),
            [new(Vector3.Zero, Vector3.Zero, 0.1f, 1)], 0, settings));
    }

    [TestMethod]
    public void UnresolvedMotionExhaustsBudgetWithHazardId()
    {
        var settings = new SimulationSettings(moveSpeed: 0.1f, jumpSpeed: 0.1f,
            boostMultiplier: 1f, maxFallSpeed: 0.1f, maxCollisionSubsteps: 1);
        var point = new Vector3(0, 0.65001f, 0);
        var ex = Assert.ThrowsExactly<InvalidOperationException>(() => HazardCollision.IntersectsPath(
            Beam(1f), [new(point, point, 0, 1)], 0, settings));
        StringAssert.Contains(ex.Message, "beam");
    }
}
