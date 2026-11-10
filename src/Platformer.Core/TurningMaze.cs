using System.Numerics;

namespace Platformer.Core;

/// <summary>Данные второй локации; физика и представление используют общие OBB.</summary>
public static class TurningMaze
{
    public static LevelDefinition Create()
    {
        const float slope = MathF.PI / 9f;
        float height = 3f * MathF.Tan(slope);
        var rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, slope);
        var normal = Vector3.Transform(Vector3.UnitY, rotation);
        var ramp = new OrientedBox(new Vector3(25.5f, height / 2f, 8f) - normal * 0.2f,
            new Vector3(1.5f / MathF.Cos(slope), 0.2f, 1.25f), rotation);
        SurfaceDefinition[] surfaces =
        [
            Flat("start", 0, 0, 0, 4, 4),
            Flat("approach", 5, 0, 0, 6, 2.5f),
            Flat("junction", 10, 0, 0, 4, 4),
            Flat("dead-end", 10, 0, -4.5f, 2.5f, 5),
            Flat("turn-one", 10, 0, 5, 2.5f, 6),
            Flat("turn-two", 13, 0, 8, 6, 2.5f),
            Flat("arena", 20, 0, 8, 8, 8),
            new("ramp", ramp),
            Flat("takeoff", 29, height, 8, 4, 4),
            Flat("landing", 35, height, 8, 4, 4),
            Flat("finish-platform", 39, height, 8, 4, 4),
            Rail("rail-approach-south", new(5, 0.45f, -1.32f), new(6, 0.9f, 0.14f)),
            Rail("rail-approach-north", new(5, 0.45f, 1.32f), new(6, 0.9f, 0.14f)),
            Rail("rail-dead-end-west", new(8.68f, 0.45f, -4.5f), new(0.14f, 0.9f, 5)),
            Rail("rail-dead-end-east", new(11.32f, 0.45f, -4.5f), new(0.14f, 0.9f, 5)),
            Rail("rail-turn-west", new(8.68f, 0.45f, 5), new(0.14f, 0.9f, 6)),
            Rail("rail-turn-north", new(13.5f, 0.45f, 9.32f), new(5, 0.9f, 0.14f)),
        ];
        CoinDefinition[] coins =
        [
            Coin("bonus-1", 10, 0.8f, -3), Coin("bonus-2", 10, 0.8f, -4.5f),
            Coin("bonus-3", 10, 0.8f, -6), Coin("main-01", 0.5f, 0.8f, -0.5f),
            Coin("main-02", 3.5f, 0.8f, 0), Coin("main-03", 6.5f, 0.8f, 0),
            Coin("main-04", 10, 0.8f, 0), Coin("main-05", 10, 0.8f, 4),
            Coin("main-06", 10, 0.8f, 7.5f), Coin("main-07", 13.5f, 0.8f, 8),
            Coin("main-08", 16.8f, 0.8f, 8), Coin("main-09", 23.4f, 0.8f, 8),
            Coin("main-10", 25.5f, height / 2f + 0.8f, 8),
            Coin("main-11", 29, height + 0.8f, 8), Coin("main-12", 35, height + 0.8f, 8),
        ];
        var hazard = new HazardDefinition("arena-beam", new Vector3(20, 0.55f, 8), Vector3.UnitY,
            Vector3.Zero, new Vector3(3.2f, 0.22f, 0.22f), Quaternion.Identity, 1, 0, HazardResponse.Knockback);
        var finish = new OrientedBox(new Vector3(39, height + 0.75f, 8), new Vector3(1.5f, 0.75f, 0.1f),
            Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 2f));
        return new LevelDefinition("level2", new Vector3(-1, 0.501f, 0), -5.4f,
            finish, surfaces, coins, [hazard]);
    }

    private static SurfaceDefinition Flat(string id, float x, float top, float z, float width, float length) =>
        new(id, new OrientedBox(new Vector3(x, top - 0.2f, z),
            new Vector3(width / 2f, 0.2f, length / 2f), Quaternion.Identity));

    private static CoinDefinition Coin(string id, float x, float y, float z) =>
        new(id, new Vector3(x, y, z), 0.2f);

    private static SurfaceDefinition Rail(string id, Vector3 center, Vector3 size) =>
        new(id, new(center, size / 2f, Quaternion.Identity), isBarrier: true);
}
