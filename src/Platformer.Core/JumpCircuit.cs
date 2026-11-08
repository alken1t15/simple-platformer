using System.Numerics;

namespace Platformer.Core;

/// <summary>Дополнительный маршрут: три смертельных барьера и узкий мост.</summary>
public static class JumpCircuit
{
    public static LevelDefinition Create()
    {
        SurfaceDefinition[] surfaces =
        [
            Flat("start", 0, 0, 4, 4), Flat("runway", 10, 0, 16, 3),
            Flat("turn", 20, 0, 4, 4), Flat("bridge", 20, 5, 1.4f, 6), Flat("finish-platform", 20, 10, 4, 4),
        ];
        CoinDefinition[] coins =
        [
            Coin("start-coin", 0.5f, 0.8f, 0), Coin("approach-coin", 2.7f, 0.8f, 0),
            Coin("jump-one", 5.5f, 1.35f, 0), Coin("rest-one", 8, 0.8f, 0),
            Coin("jump-two", 10, 1.35f, 0), Coin("rest-two", 12.5f, 0.8f, 0),
            Coin("jump-three", 14.5f, 1.35f, 0), Coin("turn-coin", 20, 0.8f, 0),
            Coin("bridge-coin", 20, 0.8f, 5), Coin("finish-coin", 20, 0.8f, 10),
        ];
        var walls = new[] { 5.5f, 10f, 14.5f }.Select((x, i) => new HazardDefinition($"jump-wall-{i + 1}",
            new(x, 0.25f, 0), Vector3.UnitY, Vector3.Zero, new(0.1f, 0.25f, 1.7f), Quaternion.Identity, 0, 0)).ToArray();
        return new("jump-circuit", new(-1, 0.501f, 0), -5.4f,
            new(new(20, 0.75f, 10), new(1.5f, 0.75f, 0.1f), Quaternion.Identity), surfaces, coins, walls);
    }

    private static SurfaceDefinition Flat(string id, float x, float z, float width, float depth) =>
        new(id, new(new(x, -0.2f, z), new(width / 2, 0.2f, depth / 2), Quaternion.Identity));
    private static CoinDefinition Coin(string id, float x, float y, float z) => new(id, new(x, y, z), 0.2f);
}
