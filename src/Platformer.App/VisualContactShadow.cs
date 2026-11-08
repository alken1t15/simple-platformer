using System.Numerics;
using Engine3D.Core;
using Platformer.Core;

namespace Platformer.App;

/// <summary>Декоративное контактное затемнение на непрозрачном материале дороги.</summary>
internal sealed class VisualContactShadow
{
    private const int Phases = 8;
    private readonly Scene _scene;
    private readonly VisualRoadLayout _layout;
    private readonly TextureData[] _frames;
    private readonly MaterialData _material = new();
    private readonly Dictionary<(int Surface, int X, int Z), SceneObject> _parts = new();
    private SceneObject? _visible;

    public VisualContactShadow(Scene scene, VisualRoadLayout layout, TextureData[] frames)
    {
        _scene = scene; _layout = layout; _frames = frames;
        for (int i = 0; i < layout.Patches.Count; i++)
            foreach (var cell in layout.Patches[i].Shadows)
                _parts.Add((i, cell.Key.X, cell.Key.Z), new(cell.Value, _material));
    }

    public void Apply(BallState ball, IReadOnlySet<int>? hidden = null)
    {
        SceneObject? selected = null;
        float bestGap = 0.025f;
        if (ball.Grounded)
        {
            Vector3 foot = ball.Center - ball.GroundNormal * ball.Radius;
            for (int i = 0; i < _layout.Patches.Count; i++)
            {
                if (hidden?.Contains(i) == true) continue;
                var patch = _layout.Patches[i];
                if (Vector3.Dot(patch.Normal, ball.GroundNormal) < 0.98f) continue;
                float gap = MathF.Abs(Vector3.Dot(foot, patch.Normal) - patch.Height);
                Vector2 p = patch.Project(foot);
                if (gap >= bestGap || !patch.Contains(p)) continue;
                Vector2 cell = p / VisualRoadLayout.Pitch;
                int x = (int)MathF.Floor(cell.X), z = (int)MathF.Floor(cell.Y);
                if (!_parts.TryGetValue((i, x, z), out var part)) continue;
                int px = Math.Clamp((int)((cell.X - x) * Phases), 0, Phases - 1);
                int pz = Math.Clamp((int)((cell.Y - z) * Phases), 0, Phases - 1);
                _material.Texture = _frames[(((x + z) & 1) * Phases + pz) * Phases + px];
                selected = part; bestGap = gap;
            }
        }
        if (ReferenceEquals(selected, _visible)) return;
        if (_visible is not null) _scene.Remove(_visible);
        if (selected is not null) _scene.Add(selected);
        _visible = selected;
    }

    // 128 общих кадров создаются один раз; Apply выбирает текстуру и готовый обрезанный меш.
    public static TextureData[] CreateFrames(TextureData tiles)
    {
        const int resolution = 192;
        var result = new TextureData[2 * Phases * Phases];
        for (int parity = 0; parity < 2; parity++)
        for (int phaseZ = 0; phaseZ < Phases; phaseZ++)
        for (int phaseX = 0; phaseX < Phases; phaseX++)
        {
            var pixels = new byte[resolution * resolution * 4];
            for (int y = 0; y < resolution; y++)
            for (int x = 0; x < resolution; x++)
            {
                float tx = (x + 0.5f) / 64, tz = (y + 0.5f) / 64;
                int cx = (int)tx, cz = (int)tz;
                float dx = (tx - 1 - (phaseX + 0.5f) / Phases) / 0.95f;
                float dz = (tz - 1 - (phaseZ + 0.5f) / Phases) / 0.86f;
                float fade = MathF.Max(0, 1 - dx * dx - dz * dz);
                Vector3 color = Sample(tiles, 0.78f + (tx - cx) * 61.44f + (((parity + cx + cz) & 1) * 64),
                    0.78f + (tz - cz) * 61.44f) * (1 - 0.48f * fade * fade);
                int at = (y * resolution + x) * 4;
                pixels[at] = (byte)color.X;
                pixels[at + 1] = (byte)color.Y;
                pixels[at + 2] = (byte)color.Z;
                pixels[at + 3] = 255;
            }
            result[(parity * Phases + phaseZ) * Phases + phaseX] = new(resolution, resolution, pixels);
        }
        return result;
    }

    private static Vector3 Sample(TextureData texture, float x, float y)
    {
        int ix = (int)x, iy = (int)y;
        return Vector3.Lerp(Vector3.Lerp(Pixel(texture, ix, iy), Pixel(texture, ix + 1, iy), x - ix),
            Vector3.Lerp(Pixel(texture, ix, iy + 1), Pixel(texture, ix + 1, iy + 1), x - ix), y - iy);
    }

    private static Vector3 Pixel(TextureData texture, int x, int y)
    {
        int at = (y * texture.Width + x) * 4;
        return new(texture.Pixels[at], texture.Pixels[at + 1], texture.Pixels[at + 2]);
    }

}
