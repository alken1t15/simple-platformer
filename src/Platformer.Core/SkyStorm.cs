using System.Numerics;

namespace Platformer.Core;

/// <summary>Поднимающаяся петля, путепровод и рассыпающиеся опоры финального испытания.</summary>
public static class SkyStorm
{
    public static LevelDefinition Create()
    {
        const float height = 4.5f;
        var surfaces = new List<SurfaceDefinition>
        {
            Flat("start", -6, 0, 0, 4, 4), Flat("runway", 1, 0, 0, 14.2f, 2.2f),
            Flat("loop-exit", 0, height, 8, 3.2f, 3.2f),
            Flat("overpass", 11, height, 8, 22, 2.1f), Flat("takeoff", 22, height, 8, 4, 3.2f),
            Flat("crumb-one", 26, height, 8, 2, 2, 1.25f),
            Flat("crumb-two", 29, height, 8, 2, 2, 1.25f),
            Flat("crumb-three", 32, height, 8, 2, 2, 1.25f),
            Flat("landing", 35, height, 8, 3.2f, 3.2f), Flat("arena", 40, height, 8, 8, 7),
            Flat("exit-bridge", 44.5f, height, 10.3f, 1.4f, 2),
            Flat("finish-platform", 48, height, 8, 7, 7),
        };
        const int segments = 36;
        for (int i = 0; i < segments; i++)
        {
            Vector3 p = LoopPoint((float)i / segments), q = LoopPoint((float)(i + 1) / segments);
            Vector3 x = Vector3.Normalize(q - p);
            Vector3 y = Vector3.Normalize(Vector3.UnitY - x * x.Y);
            Vector3 z = Vector3.Cross(x, y);
            var orientation = Quaternion.CreateFromRotationMatrix(new Matrix4x4(
                x.X, x.Y, x.Z, 0, y.X, y.Y, y.Z, 0, z.X, z.Y, z.Z, 0, 0, 0, 0, 1));
            surfaces.Add(new($"loop-{i:00}", new((p + q) / 2 - y * 0.2f,
                new(Vector3.Distance(p, q) / 2 + 0.04f, 0.2f, 1.05f), orientation)));
        }
        var coins = new List<CoinDefinition>
        {
            Coin("start-a", new(-6, 0, 0)), Coin("start-b", new(-4.5f, 0, 0)),
            Coin("runway-a", new(1, 0, 0)), Coin("runway-b", new(6, 0, 0)),
        };
        for (int i = 6; i <= segments; i += 6) coins.Add(Coin($"loop-coin-{i}", LoopPoint((float)i / segments)));
        foreach (float x in new[] { 4f, 12f, 20f }) coins.Add(Coin($"bridge-{x}", new(x, height, 8)));
        foreach (float x in new[] { 26f, 29f, 32f }) coins.Add(Coin($"crumb-{x}", new(x, height, 8)));
        coins.Add(Coin("landing-coin", new(35, height, 8)));
        coins.Add(Coin("arena-coin", new(40, height, 10.3f)));
        coins.Add(Coin("finish-a", new(46, height, 10.3f)));
        coins.Add(Coin("finish-b", new(49, height, 8)));
        HazardDefinition[] hazards =
        [
            new("final-wall", new(38, height + 0.25f, 8), Vector3.UnitY, Vector3.Zero,
                new(0.1f, 0.25f, 3.6f), Quaternion.Identity, 0, 0),
            new("final-beam", new(43, height + 0.55f, 8), Vector3.UnitY, Vector3.Zero,
                new(1.4f, 0.15f, 0.15f), Quaternion.Identity, -0.9f, 0.4f),
        ];
        return new("sky-storm", new(-7, 0.501f, 0), -6,
            new(new(50.5f, height + 0.75f, 8), new(0.1f, 0.75f, 1.4f), Quaternion.Identity), surfaces, coins, hazards);
    }

    private static Vector3 LoopPoint(float fraction)
    {
        float angle = -MathF.PI / 2 + fraction * 1.5f * MathF.PI;
        return new(8 + 8 * MathF.Cos(angle), MathF.Min(1, fraction * 1.2f) * 4.5f, 8 + 8 * MathF.Sin(angle));
    }

    private static SurfaceDefinition Flat(string id, float x, float top, float z, float width, float depth, float delay = 0) =>
        new(id, new(new(x, top - 0.2f, z), new(width / 2, 0.2f, depth / 2), Quaternion.Identity), delay);
    private static CoinDefinition Coin(string id, Vector3 floor) => new(id, floor + Vector3.UnitY * 0.8f, 0.2f);
}
