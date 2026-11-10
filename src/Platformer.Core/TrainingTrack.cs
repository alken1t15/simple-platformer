using System.Numerics;

namespace Platformer.Core;

/// <summary>Первая локация: данные одного маршрута, без физики и ресурсов рендера.</summary>
public static class TrainingTrack
{
    public static LevelDefinition Create()
    {
        const float slope = MathF.PI / 9f;
        const float thickness = 0.4f;
        float height = 3f * MathF.Tan(slope);
        Quaternion rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, slope);
        Vector3 normal = Vector3.Transform(Vector3.UnitY, rotation);
        var ramp = new OrientedBox(new Vector3(3.5f, height / 2f, 0f) - normal * (thickness / 2f),
            new Vector3(3f / MathF.Cos(slope) / 2f, thickness / 2f, 1.25f), rotation);

        SurfaceDefinition[] surfaces =
        [
            Flat("start", 0f, 0f, 0f, 4f, 4f),
            new("ramp", ramp),
            Flat("upper-platform", 7f, height, 0f, 4f, 4f),
            Flat("bridge", 7f, height, 5f, 1.4f, 6f),
            Flat("jump-takeoff", 7f, height, 10f, 4f, 4f),
            Flat("jump-landing", 7f, height, 16f, 4f, 4f),
            Flat("boost-straight", 1f, height, 16f, 8f, 2.5f),
            Flat("finish-platform", -5f, height, 16f, 4f, 4f),
        ];
        CoinDefinition[] coins =
        [
            new("coin-start-1", new Vector3(-0.3f, 0.8f, -0.65f), 0.2f),
            new("coin-start-2", new Vector3(0.9f, 0.8f, 0.5f), 0.2f),
            new("coin-ramp-1", new Vector3(2.9f, 0.9f * MathF.Tan(slope) + 0.8f, 0f), 0.2f),
            new("coin-ramp-2", new Vector3(4.1f, 2.1f * MathF.Tan(slope) + 0.8f, 0f), 0.2f),
            new("coin-bridge-1", new Vector3(7f, height + 0.8f, 4f), 0.2f),
            new("coin-bridge-2", new Vector3(7f, height + 0.8f, 6f), 0.2f),
            new("coin-jump-1", new Vector3(7f, height + 0.8f, 11f), 0.2f),
            new("coin-jump-2", new Vector3(7f, height + 0.8f, 15f), 0.2f),
            new("coin-boost-1", new Vector3(3f, height + 0.8f, 16f), 0.2f),
            new("coin-boost-2", new Vector3(0f, height + 0.8f, 16f), 0.2f),
        ];
        var finish = new OrientedBox(new Vector3(-5f, height + 0.75f, 16f),
            new Vector3(1.5f, 0.75f, 0.1f), Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 2f));
        return new LevelDefinition("level1", new Vector3(-1f, 0.501f, 0f), -5.4f,
            finish, surfaces, coins, Array.Empty<HazardDefinition>());
    }

    private static SurfaceDefinition Flat(string id, float x, float top, float z, float width, float length) =>
        new(id, new OrientedBox(new Vector3(x, top - 0.2f, z),
            new Vector3(width / 2f, 0.2f, length / 2f), Quaternion.Identity));
}
