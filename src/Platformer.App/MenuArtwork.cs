using System.Numerics;
using Engine3D.Core;

namespace Platformer.App;

/// <summary>Общие процедурные значки рейтинга, не зависят от ASCII-атласа движка.</summary>
internal static class MenuArtwork
{
    public static TextureData FilledStar { get; } = Star(filled: true);
    public static TextureData EmptyStar { get; } = Star(filled: false);

    private static TextureData Star(bool filled)
    {
        const int size = 32;
        byte[] pixels = new byte[size * size * 4];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            Vector2 p = new((x + 0.5f - size / 2) / (size * 0.7f), (size / 2 - y - 0.5f) / (size * 0.7f));
            if (!SkinTextures.InsideStar(p)) continue;
            bool rim = !SkinTextures.InsideStar(p / 0.82f);
            if (!filled && !rim) continue;
            int at = (y * size + x) * 4;
            pixels[at] = filled ? (byte)255 : (byte)121;
            pixels[at + 1] = filled ? (rim ? (byte)173 : (byte)219) : (byte)142;
            pixels[at + 2] = filled ? (byte)63 : (byte)163;
            pixels[at + 3] = 255;
        }
        return new(size, size, pixels);
    }
}
