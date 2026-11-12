using System.Numerics;
using Engine3D.Core;

namespace Platformer.App;

/// <summary>Небольшие собственные CPU-значки; не загружают файлы и не создают GPU-ресурсы.</summary>
internal static class DialogArtwork
{
    internal static TextureData Pause { get; } = Badge(0);
    internal static TextureData Retry { get; } = Badge(1);
    internal static TextureData Finish { get; } = Badge(2);

    private static TextureData Badge(int kind)
    {
        const int size = 96;
        byte[] pixels = new byte[size * size * 4];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            Vector2 p = new((x + 0.5f - size / 2) / (size / 2), (size / 2 - y - 0.5f) / (size / 2));
            float radius = p.Length();
            bool shape = kind switch
            {
                0 => MathF.Abs(p.Y) < 0.36f && MathF.Abs(p.X) is > 0.12f and < 0.29f,
                1 => (radius is > 0.28f and < 0.41f && !(p.X < -0.1f && p.Y > 0.15f))
                    || (p.X is > -0.54f and < -0.13f && p.Y > 0.1f && p.Y < 0.5f - MathF.Abs(p.X + 0.33f)),
                _ => SkinTextures.InsideStar(p * 1.25f),
            };
            Vector3 color = shape ? kind == 2 ? new(255, 218, 103) : new(193, 255, 230)
                : radius > 0.89f ? new(47, 144, 139) : new(20, 69, 77);
            int at = (y * size + x) * 4;
            pixels[at] = (byte)color.X; pixels[at + 1] = (byte)color.Y; pixels[at + 2] = (byte)color.Z;
            pixels[at + 3] = (byte)(Math.Clamp((0.97f - radius) * size, 0, 1) * 255);
        }
        return new(size, size, pixels);
    }
}
