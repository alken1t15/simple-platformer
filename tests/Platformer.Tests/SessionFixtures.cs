using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Platformer.Core;

namespace Platformer.Tests;

internal static class SessionFixtures
{
    internal static readonly GameCommand Right = new(Vector2.UnitX, false, false);
    internal static LevelDefinition Level(string id = "level1", IReadOnlyList<CoinDefinition>? coins = null,
        float finishX = 2f, bool floor = true, float floorHalfWidth = 20f) =>
        new(id, new Vector3(0f, 0.501f, 0f), -3f,
            new OrientedBox(new Vector3(finishX, 0.5f, 0f), new Vector3(0.1f, 0.75f, 1f), Quaternion.Identity),
            floor ? [new SurfaceDefinition("floor", new OrientedBox(new Vector3(0f, -0.5f, 0f),
                new Vector3(floorHalfWidth, 0.5f, 20f), Quaternion.Identity))] : [], coins ?? [], []);

    internal static HazardDefinition Beam(string id = "beam", Vector3? pivot = null,
        float speed = 0f, float angle = 0f, Vector3? halfExtents = null, Vector3? offset = null) =>
        new(id, pivot ?? new(0f, 0.501f, 0f), Vector3.UnitY, offset ?? Vector3.Zero,
            halfExtents ?? new(2f, 0.15f, 0.15f), Quaternion.Identity, speed, angle);

    internal static LevelDefinition WithHazards(LevelDefinition level, params HazardDefinition[] hazards) =>
        new(level.Id, level.Spawn, level.KillY, level.Finish, level.Surfaces, level.Coins, hazards);

    internal static LevelDefinition RotatingContactLevel(SimulationSettings settings)
    {
        var basis = Level(finishX: 50f);
        var hazard = Beam(speed: (MathF.PI / 2f) / settings.FixedStepSeconds, angle: -3f * MathF.PI / 4f);
        return new(basis.Id, new(1.8f, 0.501f, 0f), basis.KillY, basis.Finish, basis.Surfaces, [], [hazard]);
    }

    internal static void AssertPose(HazardPose expected, HazardPose actual)
    {
        Assert.AreEqual(expected.Id, actual.Id);
        LevelDefinitionTests.Near(expected.Bounds.Center, actual.Bounds.Center);
        LevelDefinitionTests.Near(expected.Bounds.HalfExtents, actual.Bounds.HalfExtents);
        Assert.AreEqual(1f, MathF.Abs(Quaternion.Dot(expected.Bounds.Orientation, actual.Bounds.Orientation)), 1e-6f);
    }

    internal static void Until(Func<bool> done, Action step, int maximum = 500)
    {
        for (int i = 0; i < maximum && !done(); i++) step();
        Microsoft.VisualStudio.TestTools.UnitTesting.Assert.IsTrue(done(), "Expected state not reached within bounded ticks");
    }
}
