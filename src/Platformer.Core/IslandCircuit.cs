using System.Numerics;

namespace Platformer.Core;

/// <summary>Острова, дуговые мосты, подъём, вращающаяся балка и завершающий прыжок.</summary>
public static class IslandCircuit
{
    public static LevelDefinition Create()
    {
        const float slope = MathF.PI / 9;
        float height = 3 * MathF.Tan(slope);
        var surfaces = new List<SurfaceDefinition>
        {
            Flat("start", 0, 0, 0, 4, 4), Flat("island-one", 7, 0, 7, 4.5f, 4.5f),
            Flat("arena", 7, height, 14, 6, 5), Flat("takeoff", 14, height, 21, 4, 4),
            Flat("landing", 20, height, 21, 4, 4), Flat("finish-platform", 24, height, 21, 4, 4),
        };
        Arc(surfaces, "arc-one", new(0, 7), 7, -MathF.PI / 2, 0, 0);
        Arc(surfaces, "arc-two", new(14, 14), 7, MathF.PI, MathF.PI / 2, height);
        Quaternion rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitX, -slope);
        Vector3 normal = Vector3.Transform(Vector3.UnitY, rotation);
        surfaces.Add(new("ramp", new(new Vector3(7, height / 2, 10) - normal * 0.2f,
            new(1, 0.2f, 1.5f / MathF.Cos(slope)), rotation)));
        CoinDefinition[] coins =
        [
            Coin("start-coin", 0.5f, 0.8f, 0), Coin("curve-one-a", 3.5f, 0.8f, 7 - 7 * MathF.Sqrt(3) / 2),
            Coin("curve-one-b", 7 * MathF.Sqrt(3) / 2, 0.8f, 3.5f), Coin("island-coin", 7, 0.8f, 7),
            Coin("ramp-coin", 7, height / 2 + 0.8f, 10), Coin("arena-coin", 9.4f, height + 0.8f, 14),
            Coin("curve-two-a", 14 - 7 * MathF.Sqrt(3) / 2, height + 0.8f, 17.5f),
            Coin("curve-two-b", 10.5f, height + 0.8f, 14 + 7 * MathF.Sqrt(3) / 2),
            Coin("jump-coin", 14, height + 0.8f, 21), Coin("landing-coin", 20, height + 0.8f, 21),
            Coin("finish-coin", 24, height + 0.8f, 21),
        ];
        var hazard = new HazardDefinition("island-beam", new(7, height + 0.55f, 14), Vector3.UnitY,
            Vector3.Zero, new(2.2f, 0.22f, 0.22f), Quaternion.Identity, -0.8f, 0.6f, HazardResponse.Knockback);
        return new("island-circuit", new(-1, 0.501f, 0), -5.4f,
            new(new(24, height + 0.75f, 21), new(1.5f, 0.75f, 0.1f), Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 2)),
            surfaces, coins, [hazard]);
    }

    private static void Arc(List<SurfaceDefinition> surfaces, string id, Vector2 center, float radius,
        float start, float end, float top)
    {
        const int segments = 12;
        for (int i = 0; i < segments; i++)
        {
            float a = start + (end - start) * i / segments, b = start + (end - start) * (i + 1) / segments;
            Vector2 p = center + new Vector2(MathF.Cos(a), MathF.Sin(a)) * radius;
            Vector2 q = center + new Vector2(MathF.Cos(b), MathF.Sin(b)) * radius;
            Vector2 midpoint = (p + q) / 2, delta = q - p;
            surfaces.Add(new($"{id}-{i:00}", new(new(midpoint.X, top - 0.2f, midpoint.Y),
                new(delta.Length() / 2 + 0.06f, 0.2f, 0.85f),
                Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.Atan2(-delta.Y, delta.X)))));
        }
    }

    private static SurfaceDefinition Flat(string id, float x, float top, float z, float width, float depth) =>
        new(id, new(new(x, top - 0.2f, z), new(width / 2, 0.2f, depth / 2), Quaternion.Identity));
    private static CoinDefinition Coin(string id, float x, float y, float z) => new(id, new(x, y, z), 0.2f);
}
