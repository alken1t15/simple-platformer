using System.Numerics;
using Engine3D.Core;

namespace Platformer.App;

/// <summary>Собственные сферические узоры; только immutable CPU-текстуры.</summary>
internal static class SkinTextures
{
    public static TextureData RewardBall(int style) => Create((normal, u, v) =>
    {
        float wave = MathF.Sin(u * MathF.Tau * 5 + MathF.Sin(v * MathF.Tau) * 2);
        if (style == 0)
        {
            float line = MathF.Min(MathF.Abs(MathF.Sin(u * MathF.Tau * 6)), MathF.Abs(MathF.Sin(v * MathF.PI * 8)));
            return line < 0.13f ? new(90, 255, 225) : new(12, 31, 63);
        }
        if (style == 1) return Vector3.Lerp(new(42, 38, 113), new(99, 247, 213), (wave + 1) / 2)
            + new Vector3(40, 0, 42) * MathF.Sin(v * MathF.PI);
        bool belt = MathF.Abs(v - 0.5f) < 0.055f;
        float panel = u * 6 - MathF.Floor(u * 6) - 0.5f;
        bool star = InsideStar(new(panel * 2, (v - 0.5f) * 4));
        return star ? new(255, 255, 207) : belt ? new(128, 67, 11)
            : Vector3.Lerp(new(177, 92, 12), new(255, 221, 91), 0.5f + normal.Y * 0.5f);
    });

    private static readonly Vector2[] Star = Enumerable.Range(0, 10).Select(i =>
    {
        float angle = MathF.PI / 2 + i * MathF.PI / 5, radius = i % 2 == 0 ? 0.62f : 0.28f;
        return new Vector2(MathF.Cos(angle) * radius, MathF.Sin(angle) * radius);
    }).ToArray();

    public static TextureData StarBall(Vector3 paint) => Create((normal, u, v) =>
    {
        float meridian = MathF.Abs(MathF.Sin(u * MathF.Tau * 2)), belt = MathF.Abs(v - 0.5f);
        Vector3 color = paint;
        if (meridian < 0.17f || belt < 0.062f) color = new(18, 44, 71);
        if (meridian is > 0.17f and < 0.23f || belt is > 0.062f and < 0.082f) color = new(251, 255, 232);
        float panel = u * 4 - MathF.Floor(u * 4) - 0.5f;
        if (InsideStar(new(panel * 1.9f, (v - 0.28f) * 4.4f))
            || InsideStar(new(panel * 1.9f, (v - 0.72f) * 4.4f))) color = new(247, 255, 219);
        float sheen = MathF.Pow(MathF.Max(0, Vector3.Dot(normal, Vector3.Normalize(new(-0.45f, 0.64f, 0.62f)))), 5);
        float glint = MathF.Pow(MathF.Max(0, Vector3.Dot(normal, Vector3.Normalize(new(-0.36f, 0.64f, 0.68f)))), 72);
        color *= 0.82f + 0.18f * MathF.Max(normal.Y, 0);
        color = Vector3.Lerp(color, new(190, 255, 237), sheen * 0.27f);
        return Vector3.Lerp(color, new(255, 255, 247), glint * 0.97f);
    });

    public static TextureData Basketball() => Create((n, u, v) =>
    {
        // Большие круги разбивают оранжевую поверхность на восемь панелей.
        float seam = MathF.Min(MathF.Abs(n.Y), MathF.Min(MathF.Abs(n.X), MathF.Abs(n.Z)));
        float grain = 0.94f + 0.06f * MathF.Sin(u * 930) * MathF.Sin(v * 610);
        Vector3 leather = new Vector3(243, 133, 42) * grain;
        return Vector3.Lerp(new(39, 32, 27), leather, Math.Clamp((seam - 0.018f) / 0.012f, 0, 1));
    });

    public static TextureData Football()
    {
        float t = (1 + MathF.Sqrt(5)) / 2;
        Vector3[] vertices =
        [
            new(-1,t,0),new(1,t,0),new(-1,-t,0),new(1,-t,0),new(0,-1,t),new(0,1,t),
            new(0,-1,-t),new(0,1,-t),new(t,0,-1),new(t,0,1),new(-t,0,-1),new(-t,0,1),
        ];
        int[][] indices =
        [
            [0,11,5],[0,5,1],[0,1,7],[0,7,10],[0,10,11],[1,5,9],[5,11,4],[11,10,2],[10,7,6],[7,1,8],
            [3,9,4],[3,4,2],[3,2,6],[3,6,8],[3,8,9],[4,9,5],[2,4,11],[6,2,10],[8,6,7],[9,8,1],
        ];
        var faces = indices.Select(i => new Face(Vector3.Normalize(vertices[i[0]]),
            Vector3.Normalize(vertices[i[1]]), Vector3.Normalize(vertices[i[2]]))).ToArray();
        return Create((n, _, _) =>
        {
            var face = faces[0];
            float dot = -1;
            foreach (var candidate in faces)
            {
                float d = Vector3.Dot(candidate.Normal, n);
                if (d > dot) { dot = d; face = candidate; }
            }
            Vector3 p = n * (Vector3.Dot(face.Normal, face.A) / dot) - face.A;
            float d20 = Vector3.Dot(p, face.U), d21 = Vector3.Dot(p, face.V);
            float b = (face.VV * d20 - face.UV * d21) / face.Determinant;
            float c = (face.UU * d21 - face.UV * d20) / face.Determinant, a = 1 - b - c;
            float max = MathF.Max(a, MathF.Max(b, c)), min = MathF.Min(a, MathF.Min(b, c));
            bool black = max > 2f / 3f;
            bool stitch = MathF.Abs(max - 2f / 3f) < 0.009f || (!black && min < 0.009f);
            return stitch ? new(97, 111, 109) : black ? new(24, 42, 48) : new(253, 251, 235);
        });
    }

    private sealed class Face
    {
        public Vector3 A { get; }
        public Vector3 U { get; }
        public Vector3 V { get; }
        public Vector3 Normal { get; }
        public float UU { get; }
        public float UV { get; }
        public float VV { get; }
        public float Determinant => UU * VV - UV * UV;
        public Face(Vector3 a, Vector3 b, Vector3 c)
        {
            A = a; U = b - a; V = c - a;
            Normal = Vector3.Normalize(Vector3.Cross(U, V));
            UU = Vector3.Dot(U, U); UV = Vector3.Dot(U, V); VV = Vector3.Dot(V, V);
        }
    }

    public static bool InsideStar(Vector2 p)
    {
        bool inside = false;
        Vector2 previous = Star[^1];
        foreach (var current in Star)
        {
            if ((current.Y > p.Y) != (previous.Y > p.Y)
                && p.X < (previous.X - current.X) * (p.Y - current.Y) / (previous.Y - current.Y) + current.X)
                inside = !inside;
            previous = current;
        }
        return inside;
    }

    public static TextureData Preview(MaterialData material)
    {
        const int size = 96;
        var pixels = new byte[size * size * 4];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float nx = (x + 0.5f - size / 2f) / (size / 2f - 2), ny = (size / 2f - y - 0.5f) / (size / 2f - 2);
            float radius = nx * nx + ny * ny;
            if (radius >= 1) continue;
            Vector3 normal = new(nx, ny, MathF.Sqrt(1 - radius));
            Vector3 local = Vector3.Transform(normal, Quaternion.CreateFromAxisAngle(Vector3.UnitY, 0.4f));
            var tint = material.BaseColor;
            Vector3 color = new Vector3(tint.R, tint.G, tint.B) * 255;
            if (material.Texture is { } texture)
            {
                float u = MathF.Atan2(local.Z, local.X) / MathF.Tau;
                if (u < 0) u += 1;
                int tx = Math.Clamp((int)(u * texture.Width), 0, texture.Width - 1);
                int ty = Math.Clamp((int)(MathF.Acos(Math.Clamp(local.Y, -1, 1)) / MathF.PI * (texture.Height - 1)), 0, texture.Height - 1);
                int source = (ty * texture.Width + tx) * 4;
                color *= new Vector3(texture.Pixels[source], texture.Pixels[source + 1], texture.Pixels[source + 2]) / 255;
            }
            color *= 0.59f + 0.41f * MathF.Max(0, Vector3.Dot(normal, Vector3.Normalize(new(-0.5f, 0.8f, 0.7f))));
            int at = (y * size + x) * 4;
            pixels[at] = (byte)color.X; pixels[at + 1] = (byte)color.Y; pixels[at + 2] = (byte)color.Z;
            pixels[at + 3] = (byte)(Math.Clamp((1 - radius) * 35, 0, 1) * 255);
        }
        return new(size, size, pixels);
    }

    private static TextureData Create(Func<Vector3, float, float, Vector3> sample)
    {
        const int width = 768, height = 384;
        var pixels = new byte[width * height * 4];
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            float u = x / (float)width, v = 1 - y / (float)(height - 1);
            float latitude = (1 - v) * MathF.PI, longitude = u * MathF.Tau;
            Vector3 n = new(MathF.Sin(latitude) * MathF.Cos(longitude), MathF.Cos(latitude), MathF.Sin(latitude) * MathF.Sin(longitude));
            Vector3 color = Vector3.Clamp(sample(n, u, v), Vector3.Zero, new(255));
            int at = (y * width + x) * 4;
            pixels[at] = (byte)color.X; pixels[at + 1] = (byte)color.Y; pixels[at + 2] = (byte)color.Z; pixels[at + 3] = 255;
        }
        return new(width, height, pixels);
    }
}
