using System.Numerics;
using Engine3D.Core;

namespace Platformer.App;

internal static partial class SkinTextures
{
    public static TextureData Watermelon() => Create((n, u, v) =>
    {
        float stripe = MathF.Sin(u * MathF.Tau * 11 + MathF.Sin(v * MathF.PI * 5) * 0.55f);
        Vector3 rind = Vector3.Lerp(new(25, 93, 58), new(110, 194, 68), (stripe + 1) / 2);
        if (n.Z < 0.34f) return rind;
        if (n.Z < 0.43f) return new(225, 241, 153);
        float sx = u * 18 - MathF.Floor(u * 18) - 0.5f;
        float sy = v * 12 - MathF.Floor(v * 12) - 0.5f;
        return sx * sx * 6 + sy * sy * 2 < 0.075f ? new(49, 36, 39)
            : Vector3.Lerp(new(245, 76, 91), new(255, 137, 126), (n.Y + 1) / 2);
    });

    public static TextureData Porcelain() => Create((n, u, v) =>
    {
        float wave = MathF.Abs(MathF.Sin(u * MathF.Tau * 4 + MathF.Sin(v * MathF.PI * 6) * 0.8f));
        bool rim = MathF.Abs(v - 0.16f) < 0.012f || MathF.Abs(v - 0.84f) < 0.012f;
        if (rim) return new(220, 173, 72);
        return wave < 0.14f ? new(34, 76, 153) : wave < 0.27f ? new(100, 154, 211)
            : new Vector3(248, 245, 226) * (0.95f + 0.05f * MathF.Max(0, n.Y));
    });

    public static TextureData Magma() => Create((n, u, v) =>
    {
        float a = MathF.Abs(MathF.Sin(u * MathF.Tau * 7 + MathF.Sin(v * MathF.PI * 7) * 1.3f));
        float b = MathF.Abs(MathF.Sin(v * MathF.PI * 9 + MathF.Sin(u * MathF.Tau * 4) * 0.7f));
        float crack = MathF.Min(a, b);
        float grain = MathF.Sin(n.X * 119 + n.Z * 63) * MathF.Sin(n.Y * 93);
        if (crack < 0.035f) return new(255, 231, 118);
        if (crack < 0.11f) return Vector3.Lerp(new(255, 183, 49), new(238, 73, 29), (crack - 0.035f) / 0.075f);
        return new Vector3(48, 42, 57) * (0.84f + grain * 0.16f);
    });

    private static readonly Vector3[] DiscoPalette = [new(94, 224, 223), new(247, 145, 203),
        new(249, 216, 112), new(160, 151, 247), new(146, 230, 171)];

    public static TextureData Disco() => Create((n, u, v) =>
    {
        float column = u * 12, row = v * 9;
        float x = column - MathF.Floor(column), y = row - MathF.Floor(row);
        float edge = MathF.Min(MathF.Min(x, 1 - x), MathF.Min(y, 1 - y));
        if (edge < 0.035f) return new(31, 48, 68);
        Vector3 tile = DiscoPalette[((int)column * 3 + (int)row * 2) % DiscoPalette.Length];
        float gleam = MathF.Pow(MathF.Max(0, 1 - Vector2.Distance(new(x, y), new(0.26f, 0.73f)) * 2), 6);
        return Vector3.Lerp(tile * (0.78f + x * 0.22f), new(255, 255, 246), gleam * 0.9f);
    });

    public static TextureData Voyager() => Create((n, u, v) =>
    {
        float nebula = (MathF.Sin(u * MathF.Tau * 3 + v * 8) + 1) / 2;
        Vector3 space = Vector3.Lerp(new(16, 28, 64), new(98, 47, 133), nebula * MathF.Sin(v * MathF.PI));
        float orbit = MathF.Abs(n.Y + MathF.Sin(u * MathF.Tau * 2) * 0.24f);
        if (orbit < 0.025f) return new(104, 239, 228);
        float x = u * 10 - MathF.Floor(u * 10) - 0.5f;
        float y = v * 6 - MathF.Floor(v * 6) - 0.5f;
        if (InsideStar(new(x * 4, y * 4))) return new(255, 231, 157);
        return space;
    });
}
