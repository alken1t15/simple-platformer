using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Platformer.Core;

namespace Platformer.Tests;

[TestClass]
public sealed class SphereGeometryTests
{
    private static OrientedBox Box => new(Vector3.Zero, Vector3.One, Quaternion.Identity);

    [TestMethod]
    public void ContactNormalsAndDepthMatchAnalyticFaceEdgeAndCornerExamples()
    {
        var face = SphereGeometry.Contact(Box, new Vector3(1.4f, 0f, 0f), 0.5f);
        Near(Vector3.UnitX, face.Normal);
        Assert.AreEqual(-0.1f, face.Separation, 1e-6f);
        var edge = SphereGeometry.Contact(Box, new Vector3(1.3f, 1.4f, 0f), 0.5f);
        Near(new Vector3(0.6f, 0.8f, 0f), edge.Normal);
        Assert.AreEqual(0f, edge.Separation, 1e-6f);
        var corner = SphereGeometry.Contact(Box, new Vector3(1.2f, 1.3f, 1.6f), 0.5f);
        Near(new Vector3(2f, 3f, 6f) / 7f, corner.Normal);
        Assert.AreEqual(0.2f, corner.Separation, 1e-6f);
    }

    [TestMethod]
    public void InteriorTieSelectsPositiveXThenYThenZ()
    {
        var center = SphereGeometry.Contact(Box, Vector3.Zero, 0.5f);
        Near(Vector3.UnitX, center.Normal);
        Assert.AreEqual(-1.5f, center.Separation);
        Near(Vector3.UnitY, SphereGeometry.Contact(Box, new Vector3(0f, 0.2f, 0.2f), 0.5f).Normal);
        Near(-Vector3.UnitZ, SphereGeometry.Contact(Box, new Vector3(0f, 0f, -0.3f), 0.5f).Normal);
    }

    [TestMethod]
    public void SweepFindsAnalyticFaceEdgeAndCornerEntryTimes()
    {
        Hit(new Vector3(3f, 0f, 0f), new Vector3(-4f, 0f, 0f), 0.375d);
        Hit(new Vector3(3f, 1.3f, 0f), new Vector3(-3f, 0f, 0f), (3d - 1.4d) / 3d);
        Hit(new Vector3(3f, 1.3f, 1.2f), new Vector3(-3f, 0f, 0f), (2d - Math.Sqrt(0.12d)) / 3d);
    }

    [TestMethod]
    public void ExpandedBoxCornerDoesNotCreateAFalseSphereContact()
    {
        Assert.IsFalse(SphereGeometry.Sweep(Box, new Vector3(3f, 1.49f, 1.49f),
            new Vector3(-6f, 0f, 0f), 0.5f, out _, out _));
    }

    [TestMethod]
    public void InitialTouchBlocksOnlyIncomingMotionAndTangentCanPass()
    {
        Assert.IsTrue(SphereGeometry.Sweep(Box, new Vector3(1.5f, 0f, 0f), -Vector3.UnitX, 0.5f, out double t, out _));
        Assert.AreEqual(0d, t);
        Assert.IsFalse(SphereGeometry.Sweep(Box, new Vector3(1.5f, 0f, 0f), Vector3.UnitX, 0.5f, out _, out _));
        Assert.IsFalse(SphereGeometry.Sweep(Box, new Vector3(1.5f, 0f, 0f), Vector3.UnitY, 0.5f, out _, out _));
        Assert.IsFalse(SphereGeometry.Sweep(Box, new Vector3(3f, 1.5f, 0f), -6f * Vector3.UnitX, 0.5f, out _, out _));
        Assert.IsFalse(SphereGeometry.Sweep(Box, Vector3.One * 3f, Vector3.Zero, 0.5f, out _, out _));
    }

    [TestMethod]
    public void SweptContactIsInvariantUnderRigidTransform()
    {
        Quaternion q = Quaternion.CreateFromYawPitchRoll(0.7f, -0.4f, 0.2f);
        var rotated = new OrientedBox(new Vector3(10f, -2f, 5f), Vector3.One, q);
        Vector3 start = rotated.ToWorld(new Vector3(3f, 1.3f, 1.2f));
        Vector3 delta = Vector3.Transform(new Vector3(-3f, 0f, 0f), rotated.Orientation);
        Assert.IsTrue(SphereGeometry.Sweep(rotated, start, delta, 0.5f, out double t, out Vector3 normal));
        Assert.AreEqual((2d - Math.Sqrt(0.12d)) / 3d, t, 2e-6d);
        Near(Vector3.Transform(new Vector3(MathF.Sqrt(0.12f), 0.3f, 0.2f) / 0.5f, q), normal);
    }

    [TestMethod]
    public void SweepCannotSkipAThinVolumeEvenWhenBothEndpointsAreClear()
    {
        var thin = new OrientedBox(Vector3.Zero, new Vector3(0.001f, 1f, 1f), Quaternion.Identity);
        Assert.IsTrue(SphereGeometry.Sweep(thin, new Vector3(-2f, 0f, 0f), 4f * Vector3.UnitX,
            0.5f, out double t, out Vector3 normal));
        Assert.AreEqual((2d - 0.501d) / 4d, t, 1e-7d);
        Near(-Vector3.UnitX, normal);
    }

    [TestMethod]
    public void SweepAgreesWithIndependentConvexDistanceSearchForSeededSegments()
    {
        var random = new Random(5);
        float Next(float scale) => ((float)random.NextDouble() * 2f - 1f) * scale;
        int compared = 0;
        for (int sample = 0; sample < 300; sample++)
        {
            var box = new OrientedBox(new Vector3(Next(5f), Next(5f), Next(5f)),
                new Vector3(0.01f + MathF.Abs(Next(2f)), 0.01f + MathF.Abs(Next(2f)), 0.01f + MathF.Abs(Next(2f))),
                Quaternion.CreateFromYawPitchRoll(Next(3f), Next(3f), Next(3f)));
            Vector3 start = box.Center + new Vector3(Next(4f), Next(4f), Next(4f));
            Vector3 delta = new(Next(8f), Next(8f), Next(8f));
            const float radius = 0.5f;
            double Distance(double fraction)
            {
                Vector3 point = start + delta * (float)fraction;
                return Vector3.DistanceSquared(point, box.ClosestPoint(point));
            }
            if (Distance(0d) <= 0.26d) continue;
            // Расстояние до выпуклого объёма вдоль отрезка выпукло. Численный
            // минимум + бисекция не используют разбиение по граням из Sweep.
            double left = 0d, right = 1d;
            for (int i = 0; i < 60; i++)
            {
                double a = (2d * left + right) / 3d, b = (left + 2d * right) / 3d;
                if (Distance(a) < Distance(b)) right = b;
                else left = a;
            }
            double minimum = (left + right) / 2d;
            double squaredDistance = Distance(minimum);
            if (Math.Abs(squaredDistance - radius * radius) < 1e-4d) continue;
            bool expected = squaredDistance < radius * radius;
            bool actual = SphereGeometry.Sweep(box, start, delta, radius, out double hit, out _);
            Assert.AreEqual(expected, actual, $"sample {sample}");
            compared++;
            if (!expected) continue;
            left = 0d;
            right = minimum;
            for (int i = 0; i < 40; i++)
            {
                double middle = (left + right) / 2d;
                if (Distance(middle) > radius * radius) left = middle;
                else right = middle;
            }
            Assert.AreEqual((left + right) / 2d, hit, 2e-5d, $"sample {sample}");
        }
        Assert.IsGreaterThan(150, compared);
    }

    private static void Hit(Vector3 start, Vector3 delta, double expected)
    {
        Assert.IsTrue(SphereGeometry.Sweep(Box, start, delta, 0.5f, out double t, out _));
        Assert.AreEqual(expected, t, 1e-6d);
        Assert.AreEqual(0.5f, Vector3.Distance(start + delta * (float)t, Box.ClosestPoint(start + delta * (float)t)), 1e-6f);
    }

    private static void Near(Vector3 expected, Vector3 actual) => LevelDefinitionTests.Near(expected, actual);
}
