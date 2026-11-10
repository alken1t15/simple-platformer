using System.Numerics;
using Engine3D.Core;

namespace Platformer.App;

internal sealed partial class VisualScene
{
    public static MaterialData GuardRail { get; } = Textured(WoodAndRope(bumper: false));
    public static MaterialData BumperBeam { get; } = Textured(WoodAndRope(bumper: true));
    public static MaterialData LethalObstacle { get; } = Textured(VolcanicStone());

    private static TextureData WoodAndRope(bool bumper) => Texture(256, 128, (x, y) =>
    {
        float u = x / 255f, v = y / 127f;
        float grain = MathF.Sin(y * 0.68f + MathF.Sin(x * 0.035f) * 1.8f);
        float fine = MathF.Sin(y * 2.7f + MathF.Sin(x * 0.077f) * 0.8f);
        float knot = MathF.Sqrt(MathF.Pow((x - 139) * 0.25f, 2) + MathF.Pow(y - 73, 2));
        float rings = MathF.Sin(knot * 0.85f) * MathF.Exp(-knot * 0.075f);
        Vector3 wood = bumper ? new(127, 79, 46) : new(188, 132, 76);
        wood *= 0.92f + grain * 0.065f + fine * 0.025f + rings * 0.15f;
        wood += new Vector3((PropNoise(x, y, 3) - 0.5f) * 9);

        // Вдоль доски идёт резной кант; семантические акценты различают
        // удерживающий борт и отбрасывающую балку, а не заменяют древесину.
        if (v is < 0.055f or > 0.945f) wood *= 0.62f;
        if (!bumper && v is > 0.12f and < 0.22f)
            wood = new Vector3(50, 152, 132) * (0.93f + grain * 0.07f);
        if (bumper && (u < 0.075f || u > 0.925f))
            wood = new Vector3(230, 113, 77) * (0.92f + fine * 0.08f);

        // Светлые многовитковые канаты оборачивают концы каждого бруса.
        float band = MathF.Min(MathF.Abs(u - 0.16f), MathF.Abs(u - 0.84f));
        if (band < 0.039f)
        {
            float strand = (x + y / 3) % 6 < 2 ? 0.78f : 1f;
            float twist = 0.92f + 0.08f * MathF.Sin(y * 0.65f + x * 0.8f);
            wood = new Vector3(238, 213, 157) * strand * twist;
            if (band > 0.033f) wood *= 0.65f;
        }
        return wood;
    });

    private static TextureData VolcanicStone()
    {
        const int width = 256, height = 128, columns = 8, rows = 4;
        var seeds = new Vector2[columns * rows];
        for (int y = 0; y < rows; y++)
        for (int x = 0; x < columns; x++)
            seeds[y * columns + x] = new(
                (x + 0.2f + PropNoise(x, y, 11) * 0.6f) * width / columns,
                (y + 0.2f + PropNoise(x, y, 17) * 0.6f) * height / rows);

        return Texture(width, height, (x, y) =>
        {
            float nearest = float.PositiveInfinity, second = float.PositiveInfinity;
            int plate = 0;
            for (int i = 0; i < seeds.Length; i++)
            {
                float dx = MathF.Abs(x - seeds[i].X), dy = MathF.Abs(y - seeds[i].Y);
                dx = MathF.Min(dx, width - dx);
                dy = MathF.Min(dy, height - dy);
                float distance = dx * dx + dy * dy;
                if (distance < nearest) { second = nearest; nearest = distance; plate = i; }
                else if (distance < second) second = distance;
            }
            // Периодические Voronoi-плиты вместо предупреждающих полос.
            float edge = MathF.Sqrt(second) - MathF.Sqrt(nearest);
            float roughness = (PropNoise(x, y, 29) - 0.5f) * 10;
            float tone = 0.82f + PropNoise(plate, 0, 31) * 0.3f;
            Vector3 stone = new Vector3(69, 63, 83) * tone + new Vector3(roughness);
            if (edge < 0.85f) return new Vector3(255, 212, 112);
            if (edge < 2.15f) return Vector3.Lerp(new(255, 173, 67), new(231, 87, 46), (edge - 0.85f) / 1.3f);
            if (edge < 5f) stone = Vector3.Lerp(new(153, 67, 49), stone, (edge - 2.15f) / 2.85f);
            return stone;
        });
    }

    private static float PropNoise(int x, int y, uint salt)
    {
        uint value = unchecked((uint)x * 374761393u + (uint)y * 668265263u + salt * 1274126177u);
        value = unchecked((value ^ (value >> 13)) * 1274126177u);
        return (value ^ (value >> 16)) / (float)uint.MaxValue;
    }
}
