using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Platformer.App;
using Platformer.Core;

namespace Platformer.Tests;

[TestClass]
public sealed class CameraRigTests
{
    internal static LevelDefinition Level(params SurfaceDefinition[] surfaces) => new("camera-test", Vector3.Zero, -10f,
        new OrientedBox(new Vector3(20f, 0f, 0f), Vector3.One, Quaternion.Identity), surfaces, [], []);

    [TestMethod]
    [DataRow(-100f, 1f)]
    [DataRow(100f, -1f)]
    public void VerticalMouseMovesViewInSameDirection(float mouseY, float expectedViewSign)
    {
        var rig = new CameraRig();
        var before = rig.Update(Vector2.Zero, Vector3.Zero, Level(), 0f);
        var after = rig.Update(new(0f, mouseY), Vector3.Zero, Level(), 0f);
        Vector3 beforeView = Vector3.Normalize(before.Target - before.Position);
        Vector3 afterView = Vector3.Normalize(after.Target - after.Position);
        Assert.IsGreaterThan(0f, (afterView.Y - beforeView.Y) * expectedViewSign,
            "Мышь вверх должна поднимать взгляд, мышь вниз — опускать.");
    }

    [TestMethod]
    public void DefaultsMouseSignsAndPitchClampMatchContract()
    {
        var rig = new CameraRig();
        Vector3 target = new(0f, 0.8f, 0f);
        CameraPose pose = rig.Update(Vector2.Zero, target, Level(), 0f);
        Assert.AreEqual(6f, Vector3.Distance(pose.Position, target), 1e-5f);
        Assert.AreEqual(0.4f, pose.PitchRadians);
        Assert.IsTrue(pose.Position.Y > target.Y && pose.Position.Z > target.Z);
        pose = rig.Update(new(100f, -100f), target, Level(), 0f);
        Assert.AreEqual(-0.3f, pose.YawRadians, 1e-6f);
        Assert.AreEqual(0.1f, pose.PitchRadians, 1e-6f);
        Assert.AreEqual(-0.35f, rig.Update(new(0f, -10000f), target, Level(), 1f).PitchRadians);
        Assert.AreEqual(1.2f, rig.Update(new(0f, 10000f), target, Level(), 1f).PitchRadians);
    }

    [TestMethod]
    public void MouseRotationIsIndependentOfFrameTimeAndTickCount()
    {
        var once = new CameraRig();
        var split = new CameraRig();
        once.Update(new(200f, 50f), Vector3.Zero, Level(), 0.1f);
        for (int i = 0; i < 10; i++) split.Update(new(20f, 5f), Vector3.Zero, Level(), 0.001f);
        Assert.AreEqual(once.YawRadians, split.YawRadians, 1e-6f);
        Assert.AreEqual(once.PitchRadians, split.PitchRadians, 1e-6f);
    }

    [TestMethod]
    public void ThinWallClipsImmediatelyAndDistanceRecoversSmoothly()
    {
        var wall = new SurfaceDefinition("wall", new OrientedBox(new(0f, 0f, 3f), new(10f, 10f, 0.001f), Quaternion.Identity));
        var rig = new CameraRig();
        CameraPose pose = rig.Update(new(0f, -CameraRig.DefaultPitch / CameraRig.MouseSensitivity), Vector3.Zero, Level(wall), 0f);
        Assert.AreEqual(2.749f, rig.Distance, 0.0001f);
        Assert.IsLessThan(3f, pose.Position.Z);
        Assert.IsGreaterThanOrEqualTo(0.249f, Vector3.Distance(pose.Position, wall.Bounds.ClosestPoint(pose.Position)));
        float clipped = rig.Distance;
        rig.Update(Vector2.Zero, Vector3.Zero, Level(), 0.01f);
        Assert.IsTrue(rig.Distance > clipped && rig.Distance < 6f);
        rig.Update(Vector2.Zero, Vector3.Zero, Level(wall), 0f);
        Assert.AreEqual(clipped, rig.Distance, 1e-6f);
    }

    [TestMethod]
    public void RoundedCornerDoesNotUseExpandedBoxFalsePositive()
    {
        var box = new SurfaceDefinition("corner", new OrientedBox(new(1.15f, 1.15f, 3f), Vector3.One, Quaternion.Identity));
        var rig = new CameraRig();
        rig.Update(new(0f, -CameraRig.DefaultPitch / CameraRig.MouseSensitivity), Vector3.Zero, Level(box), 0f);
        Assert.AreEqual(6f, rig.Distance, 0.001f);
    }

    [TestMethod]
    public void RotatedWallAndSlopeLeaveSphereOutsideGeometry()
    {
        foreach (Quaternion rotation in new[] { Quaternion.CreateFromAxisAngle(Vector3.UnitY, 0.6f),
            Quaternion.CreateFromAxisAngle(Vector3.UnitX, -0.4f) })
        {
            var wall = new SurfaceDefinition("rotated", new OrientedBox(new(0f, 1f, 3f), new(3f, 3f, 0.02f), rotation));
            var rig = new CameraRig();
            var pose = rig.Update(Vector2.Zero, Vector3.Zero, Level(wall), 0f);
            Assert.IsTrue(rig.Distance > 0f && rig.Distance < 6f);
            Assert.IsGreaterThanOrEqualTo(0.2f, Vector3.Distance(pose.Position, wall.Bounds.ClosestPoint(pose.Position)));
        }
    }

    [TestMethod]
    public void EmbeddedTargetHasDiagnosticAndNonzeroFiniteViewThenRecovers()
    {
        var rig = new CameraRig();
        var box = new SurfaceDefinition("embedded", new OrientedBox(Vector3.Zero, Vector3.One, Quaternion.Identity));
        var pose = rig.Update(Vector2.Zero, Vector3.Zero, Level(box), 0f);
        Assert.IsTrue(rig.TargetObstructed);
        Assert.AreEqual(0f, rig.Distance);
        Assert.IsTrue(float.IsFinite(pose.Position.LengthSquared()));
        Assert.IsGreaterThan(0.9f, Vector3.Distance(pose.Position, pose.Target));
        rig.Update(Vector2.Zero, new(10f, 0f, 0f), Level(box), 0.1f);
        Assert.IsFalse(rig.TargetObstructed);
        Assert.IsTrue(rig.Distance > 0f && rig.Distance < 6f);
    }

    [TestMethod]
    public void InvalidInputDoesNotMutatePoseAndHugeFiniteMouseDoesNotProduceNaN()
    {
        var rig = new CameraRig();
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => rig.Update(new(float.NaN, 0f), Vector3.Zero, Level(), 0f));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => rig.Update(Vector2.One, Vector3.Zero, Level(), -1f));
        Assert.AreEqual(0f, rig.YawRadians);
        var pose = rig.Update(new(float.MaxValue, float.MaxValue), Vector3.Zero, Level(), 1f);
        Assert.IsTrue(float.IsFinite(pose.Position.LengthSquared()));
        rig.Reset();
        Assert.AreEqual(0f, rig.YawRadians);
        Assert.AreEqual(0.4f, rig.PitchRadians);
        Assert.AreEqual(6f, rig.Distance);
    }

    [TestMethod]
    public void CastMatchesIndependentAnalyticCoreSweepForRotatedBoxes()
    {
        var random = new Random(6);
        float Next(float scale) => ((float)random.NextDouble() * 2f - 1f) * scale;
        int hits = 0;
        for (int i = 0; i < 250; i++)
        {
            Vector2 mouse = new(Next(1000f), Next(150f));
            var rig = new CameraRig();
            Vector3 desired = rig.Update(mouse, Vector3.Zero, Level(), 0f).Position;
            var box = new OrientedBox(desired * 0.5f + new Vector3(Next(1f), Next(1f), Next(1f)),
                new Vector3(0.01f + MathF.Abs(Next(1f)), 0.01f + MathF.Abs(Next(1f)), 0.01f + MathF.Abs(Next(1f))),
                Quaternion.CreateFromYawPitchRoll(Next(3f), Next(3f), Next(3f)));
            if (SphereGeometry.Contact(box, Vector3.Zero, CameraRig.ObstructionRadius).Separation <= 0.001f) continue;
            bool hit = SphereGeometry.Sweep(box, Vector3.Zero, desired, CameraRig.ObstructionRadius, out double fraction, out _);
            if (hit) hits++;
            float expected = hit ? MathF.Max(0f, 6f * (float)fraction - CameraRig.WallGap) : 6f;
            rig.Update(Vector2.Zero, Vector3.Zero, Level(new SurfaceDefinition("box", box)), 0f);
            Assert.AreEqual(expected, rig.Distance, 0.002f, $"sample {i}");
            Assert.IsFalse(rig.CastBudgetLimited, $"sample {i}");
        }
        Assert.IsGreaterThan(50, hits);
    }

    [TestMethod]
    public void NearestWallWinsRegardlessOfSurfaceOrderAndRecoveryUsesElapsedTime()
    {
        SurfaceDefinition Wall(string id, float z) => new(id,
            new OrientedBox(new(0f, 0f, z), new(10f, 10f, 0.01f), Quaternion.Identity));
        var near = Wall("near", 2f);
        var far = Wall("far", 4f);
        var a = new CameraRig();
        var b = new CameraRig();
        a.Update(Vector2.Zero, Vector3.Zero, Level(near, far), 0f);
        b.Update(Vector2.Zero, Vector3.Zero, Level(far, near), 0f);
        Assert.AreEqual(a.Distance, b.Distance, 1e-6f);
        a.Update(Vector2.Zero, Vector3.Zero, Level(), 0.1f);
        for (int i = 0; i < 10; i++) b.Update(Vector2.Zero, Vector3.Zero, Level(), 0.01f);
        Assert.AreEqual(a.Distance, b.Distance, 1e-5f);
    }
}
