using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Platformer.Core;

namespace Platformer.Tests;

[TestClass]
public sealed class LevelDefinitionTests
{
    private static OrientedBox Box => new(Vector3.Zero, Vector3.One, Quaternion.Identity);
    private static HazardDefinition Hazard(string id = "hazard") =>
        new(id, Vector3.Zero, Vector3.UnitY, Vector3.Zero, Vector3.One, Quaternion.Identity, 1f, 0f);
    private static LevelDefinition Level(IReadOnlyList<SurfaceDefinition>? surfaces = null,
        IReadOnlyList<CoinDefinition>? coins = null, IReadOnlyList<HazardDefinition>? hazards = null) =>
        new("test", new Vector3(0f, 2f, 0f), -5f, Box,
            surfaces ?? [new("surface", Box)], coins ?? [], hazards ?? []);

    [TestMethod]
    public void ObbTransformsUseNormalizedRotationAndClampInLocalSpace()
    {
        var q = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, MathF.PI / 2f);
        var box = new OrientedBox(new Vector3(10f, 20f, 30f), new Vector3(2f, 1f, 3f), q * 7f);
        Near(new Vector3(10f, 22f, 30f), box.ToWorld(new Vector3(2f, 0f, 0f)));
        Near(new Vector3(1f, 0.5f, -2f), box.ToLocal(box.ToWorld(new Vector3(1f, 0.5f, -2f))));
        Near(box.ToWorld(new Vector3(2f, 1f, -3f)), box.ClosestPoint(box.ToWorld(new Vector3(9f, 4f, -8f))));
        Near(box.Center, box.ClosestPoint(box.Center));
        Assert.AreEqual(1f, box.Orientation.Length(), 1e-6f);
    }

    [TestMethod]
    public void SlopedBoxRoundTripsAndClosestPointIsOnRotatedTopFace()
    {
        var box = new OrientedBox(new Vector3(3f, 1f, -2f), new Vector3(2f, 0.2f, 1f),
            Quaternion.CreateFromAxisAngle(Vector3.UnitZ, MathF.PI / 9f));
        Near(new Vector3(0.75f, 0.2f, 0.5f),
            box.ToLocal(box.ClosestPoint(box.ToWorld(new Vector3(0.75f, 3f, 0.5f)))));
    }

    [TestMethod]
    public void ObbSupportsRotationAroundAllAxesAndMatchesRowVectorMatrixConvention()
    {
        var rotation = Quaternion.CreateFromYawPitchRoll(0.7f, -0.4f, 0.2f);
        var center = new Vector3(2f, -3f, 5f);
        var box = new OrientedBox(center, new Vector3(2f, 1f, 3f), rotation);
        var matrix = Matrix4x4.CreateFromQuaternion(rotation) * Matrix4x4.CreateTranslation(center);
        Vector3 local = new(0.3f, -0.7f, 1.2f);
        Near(Vector3.Transform(local, matrix), box.ToWorld(local));
        Near(local, box.ToLocal(box.ToWorld(local)));
        Near(box.ToWorld(new Vector3(-2f, 1f, 3f)),
            box.ClosestPoint(box.ToWorld(new Vector3(-5f, 4f, 8f))));
    }

    [TestMethod]
    public void ObbClosestPointClampsFacesEdgesAndCorners()
    {
        Near(Vector3.UnitX, Box.ClosestPoint(new Vector3(2f, 0f, 0f)));
        Near(new Vector3(1f, -1f, 0f), Box.ClosestPoint(new Vector3(2f, -3f, 0f)));
        Near(new Vector3(-1f, 1f, 1f), Box.ClosestPoint(new Vector3(-2f, 3f, 4f)));
        Near(new Vector3(0.2f), Box.ClosestPoint(new Vector3(0.2f)));
    }

    [TestMethod]
    [DataRow(float.NaN)]
    [DataRow(float.PositiveInfinity)]
    [DataRow(float.NegativeInfinity)]
    [DataRow(10000.01f)]
    [DataRow(-10000.01f)]
    public void PositionsAndKillYRejectNonFiniteOrOutOfRangeCoordinates(float value)
    {
        var position = new Vector3(0f, value, 0f);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new OrientedBox(position, Vector3.One, Quaternion.Identity));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new CoinDefinition("coin", position, 0.2f));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new LevelDefinition("level", position, -5f, Box, [], [], []));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new LevelDefinition("level", Vector3.Zero, value, Box, [], [], []));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            new HazardDefinition("h", position, Vector3.UnitY, Vector3.Zero, Vector3.One, Quaternion.Identity, 1f, 0f));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            new HazardDefinition("h", Vector3.Zero, Vector3.UnitY, position, Vector3.One, Quaternion.Identity, 1f, 0f));
    }

    [TestMethod]
    [DataRow(0f)]
    [DataRow(-1f)]
    [DataRow(0.0009f)]
    [DataRow(10001f)]
    [DataRow(float.NaN)]
    [DataRow(float.PositiveInfinity)]
    public void SizesMustRespectContractRange(float size)
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new OrientedBox(Vector3.Zero, new Vector3(1f, size, 1f), Quaternion.Identity));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new CoinDefinition("coin", Vector3.Zero, size));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            new HazardDefinition("h", Vector3.Zero, Vector3.UnitY, Vector3.Zero, new Vector3(size, 1f, 1f), Quaternion.Identity, 1f, 0f));
    }

    [TestMethod]
    public void InclusiveNumericLimitsAreAccepted()
    {
        var box = new OrientedBox(new Vector3(-10000f, 10000f, 0f),
            new Vector3(0.001f, 10000f, 1f), Quaternion.Identity);
        Assert.AreEqual(0.001f, box.HalfExtents.X);
        Assert.AreEqual(10000f, new CoinDefinition("coin", Vector3.Zero, 10000f).Radius);
        Assert.AreEqual(0.001f, new CoinDefinition("coin", Vector3.Zero, 0.001f).Radius);
    }

    [TestMethod]
    public void QuaternionsAndAxesRejectInvalidNorms()
    {
        Quaternion[] invalid = [default, new(float.NaN, 0f, 0f, 1f),
            new(0f, 0f, 0f, float.PositiveInfinity), new(float.MaxValue, float.MaxValue, 0f, 0f)];
        foreach (var q in invalid)
        {
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new OrientedBox(Vector3.Zero, Vector3.One, q));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
                new HazardDefinition("h", Vector3.Zero, Vector3.UnitY, Vector3.Zero, Vector3.One, q, 1f, 0f));
        }
        Vector3[] axes = [Vector3.Zero, new(float.NaN, 1f, 0f), new(float.PositiveInfinity), new(float.MaxValue)];
        foreach (var axis in axes)
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
                new HazardDefinition("h", Vector3.Zero, axis, Vector3.Zero, Vector3.One, Quaternion.Identity, 1f, 0f));
    }

    [TestMethod]
    public void SmallAndLargeRepresentableNormsNormalizeWithoutIntermediateOverflow()
    {
        foreach (float magnitude in new[] { float.Epsilon, 1e-20f, 1e20f })
        {
            var box = new OrientedBox(Vector3.Zero, Vector3.One, new Quaternion(0f, 0f, 0f, magnitude));
            Assert.AreEqual(Quaternion.Identity, box.Orientation);
            var hazard = new HazardDefinition("h", Vector3.Zero, new Vector3(0f, magnitude, 0f),
                Vector3.Zero, Vector3.One, new Quaternion(0f, 0f, 0f, magnitude), -1f, -2f);
            Assert.AreEqual(Vector3.UnitY, hazard.Axis);
            Assert.AreEqual(-1f, hazard.AngularSpeed);
            Assert.AreEqual(-2f, hazard.InitialAngle);
        }
    }

    [TestMethod]
    [DataRow(float.NaN)]
    [DataRow(float.PositiveInfinity)]
    [DataRow(float.NegativeInfinity)]
    public void AngularParametersAndQueryPointsMustBeFinite(float value)
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            new HazardDefinition("h", Vector3.Zero, Vector3.UnitY, Vector3.Zero, Vector3.One, Quaternion.Identity, value, 0f));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            new HazardDefinition("h", Vector3.Zero, Vector3.UnitY, Vector3.Zero, Vector3.One, Quaternion.Identity, 0f, value));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => Box.ToLocal(new Vector3(value)));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => Box.ToWorld(new Vector3(value)));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => Box.ClosestPoint(new Vector3(value)));
    }

    [TestMethod]
    [DataRow("")]
    [DataRow(" \t")]
    public void EmptyIdsAreRejectedInEveryDefinition(string id)
    {
        Assert.ThrowsExactly<ArgumentException>(() => new SurfaceDefinition(id, Box));
        Assert.ThrowsExactly<ArgumentException>(() => new CoinDefinition(id, Vector3.Zero, 1f));
        Assert.ThrowsExactly<ArgumentException>(() => Hazard(id));
        Assert.ThrowsExactly<ArgumentException>(() => new LevelDefinition(id, Vector3.Zero, -5f, Box, [], [], []));
    }

    [TestMethod]
    public void NullArgumentsAndElementsAreRejected()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new SurfaceDefinition(null!, Box));
        Assert.ThrowsExactly<ArgumentNullException>(() => new SurfaceDefinition("s", null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => new CoinDefinition(null!, Vector3.Zero, 1f));
        Assert.ThrowsExactly<ArgumentNullException>(() => Hazard(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => new LevelDefinition(null!, Vector3.Zero, -5f, Box, [], [], []));
        Assert.ThrowsExactly<ArgumentNullException>(() => new LevelDefinition("l", Vector3.Zero, -5f, null!, [], [], []));
        Assert.ThrowsExactly<ArgumentNullException>(() => new LevelDefinition("l", Vector3.Zero, -5f, Box, null!, [], []));
        Assert.ThrowsExactly<ArgumentNullException>(() => new LevelDefinition("l", Vector3.Zero, -5f, Box, [], null!, []));
        Assert.ThrowsExactly<ArgumentNullException>(() => new LevelDefinition("l", Vector3.Zero, -5f, Box, [], [], null!));
        Assert.ThrowsExactly<ArgumentException>(() => Level(surfaces: [null!]));
        Assert.ThrowsExactly<ArgumentException>(() => Level(coins: [null!]));
        Assert.ThrowsExactly<ArgumentException>(() => Level(hazards: [null!]));
    }

    [TestMethod]
    public void DuplicateIdsAcrossAllCategoriesAreRejectedButCaseIsSignificant()
    {
        var surface = new SurfaceDefinition("same", Box);
        var coin = new CoinDefinition("same", Vector3.Zero, 0.2f);
        var hazard = Hazard("same");
        Assert.ThrowsExactly<ArgumentException>(() => Level(surfaces: [surface, surface]));
        Assert.ThrowsExactly<ArgumentException>(() => Level(coins: [coin, coin]));
        Assert.ThrowsExactly<ArgumentException>(() => Level(hazards: [hazard, hazard]));
        Assert.ThrowsExactly<ArgumentException>(() => Level([surface], [coin]));
        Assert.ThrowsExactly<ArgumentException>(() => Level([surface], hazards: [hazard]));
        Assert.ThrowsExactly<ArgumentException>(() => Level(coins: [coin], hazards: [hazard]));
        Assert.HasCount(2, Level(coins: [coin, new("Same", Vector3.Zero, 0.2f)]).Coins);
    }

    [TestMethod]
    public void AllInputListsAreCopiedAndCannotBeMutatedThroughReturnedInterfaces()
    {
        var surface = new SurfaceDefinition("surface", Box);
        SurfaceDefinition[] surfaces = [surface];
        var coin = new CoinDefinition("coin", Vector3.Zero, 0.2f);
        var coins = new List<CoinDefinition> { coin };
        var hazard = Hazard();
        HazardDefinition[] hazards = [hazard];
        var level = Level(surfaces, coins, hazards);
        surfaces[0] = new("replacement", Box);
        coins.Clear();
        hazards[0] = Hazard("replacement");
        Assert.AreSame(surface, level.Surfaces[0]);
        Assert.AreSame(coin, level.Coins[0]);
        Assert.AreSame(hazard, level.Hazards[0]);
        Assert.ThrowsExactly<NotSupportedException>(() => ((IList<SurfaceDefinition>)level.Surfaces)[0] = surfaces[0]);
        Assert.ThrowsExactly<NotSupportedException>(() => ((IList<CoinDefinition>)level.Coins).Clear());
        Assert.ThrowsExactly<NotSupportedException>(() => ((IList<HazardDefinition>)level.Hazards).Add(hazard));
        foreach (Type type in new[] { typeof(OrientedBox), typeof(SurfaceDefinition), typeof(CoinDefinition), typeof(HazardDefinition), typeof(LevelDefinition) })
            Assert.IsTrue(type.GetProperties().All(property => property.SetMethod is null), type.Name);
    }

    [TestMethod]
    public void SurfacePermutationPreservesGeometryByOrdinalId()
    {
        var original = TrainingTrack.Create();
        var reversed = Level(original.Surfaces.Reverse().ToArray());
        foreach (var surface in original.Surfaces)
        {
            var other = reversed.Surfaces.Single(candidate => candidate.Id == surface.Id);
            Assert.AreSame(surface.Bounds, other.Bounds);
            Near(surface.Bounds.ClosestPoint(original.Spawn), other.Bounds.ClosestPoint(original.Spawn));
        }
    }

    internal static void Near(Vector3 expected, Vector3 actual, float tolerance = 1e-5f) =>
        Assert.IsLessThanOrEqualTo(tolerance, Vector3.Distance(expected, actual), $"expected {expected}, actual {actual}");
}
