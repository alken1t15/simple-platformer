using System.Numerics;
using Engine3D.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Platformer.App;

namespace Platformer.Tests;

[TestClass]
public sealed class GameMeshesTests
{
    [TestMethod]
    public void SphereHasUnitRadiusNormalsValidUvsAndExactSeam()
    {
        var mesh = GameMeshes.Sphere;
        Assert.AreEqual(25 * 17, mesh.VertexCount);
        Assert.AreEqual(2 * 24 * 15, mesh.TriangleCount);
        foreach (var vertex in mesh.Vertices)
        {
            Assert.AreEqual(1f, vertex.Position.Length(), 1e-6f);
            LevelDefinitionTests.Near(vertex.Position, vertex.Normal);
            Assert.IsTrue(vertex.Uv.X is >= 0f and <= 1f && vertex.Uv.Y is >= 0f and <= 1f);
        }
        for (int row = 0; row <= 16; row++)
        {
            var left = mesh.Vertices[row * 25];
            var right = mesh.Vertices[row * 25 + 24];
            Assert.AreEqual(left.Position, right.Position);
            Assert.AreEqual(left.Normal, right.Normal);
            Assert.AreEqual(0f, left.Uv.X);
            Assert.AreEqual(1f, right.Uv.X);
        }
    }

    [TestMethod]
    public void EverySphereAndCoinTriangleHasPositiveAreaAndOutwardWinding()
    {
        foreach (var mesh in new[] { GameMeshes.Sphere, GameMeshes.Coin })
        for (int index = 0; index < mesh.IndexCount; index += 3)
        {
            var a = mesh.Vertices[(int)mesh.Indices[index]];
            var b = mesh.Vertices[(int)mesh.Indices[index + 1]];
            var c = mesh.Vertices[(int)mesh.Indices[index + 2]];
            Vector3 cross = Vector3.Cross(b.Position - a.Position, c.Position - a.Position);
            Assert.IsGreaterThan(1e-8f, cross.LengthSquared());
            Assert.IsGreaterThan(0f, Vector3.Dot(cross, a.Normal + b.Normal + c.Normal));
        }
    }

    [TestMethod]
    public void CoinIsDiscInsideTriggerSphereWithBothCaps()
    {
        var mesh = GameMeshes.Coin;
        Assert.AreEqual(96, mesh.TriangleCount);
        bool front = false, back = false;
        foreach (var vertex in mesh.Vertices)
        {
            Assert.IsLessThanOrEqualTo(1f, vertex.Position.Length());
            Assert.AreEqual(1f, vertex.Normal.Length(), 1e-6f);
            front |= vertex.Normal == Vector3.UnitZ;
            back |= vertex.Normal == -Vector3.UnitZ;
        }
        Assert.IsTrue(front && back);
    }

    [TestMethod]
    [DataRow(2, 16)]
    [DataRow(24, 1)]
    [DataRow(257, 16)]
    [DataRow(24, 257)]
    public void InvalidSphereResolutionIsRejected(int segments, int rings) =>
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => GameMeshes.CreateSphere(segments, rings));

    [TestMethod]
    public void MinimumSphereResolutionStillHasOutwardNonDegenerateFaces()
    {
        var sphere = GameMeshes.CreateSphere(3, 2);
        Assert.AreEqual(6, sphere.TriangleCount);
        for (int i = 0; i < sphere.IndexCount; i += 3)
        {
            Vector3 a = sphere.Vertices[(int)sphere.Indices[i]].Position;
            Vector3 b = sphere.Vertices[(int)sphere.Indices[i + 1]].Position;
            Vector3 c = sphere.Vertices[(int)sphere.Indices[i + 2]].Position;
            Assert.IsGreaterThan(0f, Vector3.Dot(Vector3.Cross(b - a, c - a), a + b + c));
        }
    }

    [TestMethod]
    public void SkinsHaveDifferentColorsAndShareOnePatternWithContrastingPixels()
    {
        var blue = GameSkins.Get("classic-blue");
        var orange = GameSkins.Get("orange");
        Assert.AreNotEqual(blue.Material.BaseColor, orange.Material.BaseColor);
        Assert.AreSame(blue.Material.Texture, orange.Material.Texture);
        var texture = blue.Material.Texture!;
        Assert.AreNotEqual(texture.Pixels[0], texture.Pixels[4 * 4]);
        Assert.AreEqual(255, texture.Pixels[3]);
        Assert.ThrowsExactly<KeyNotFoundException>(() => GameSkins.Get("absent"));
    }
}
