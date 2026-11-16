using System.Numerics;
using Engine3D.Core;

namespace Platformer.App;

/// <summary>Подарок целиком помещается в прежний триггер; коробка, ленты и бант — один объект.</summary>
internal static class WinterGifts
{
    public static MeshData Mesh { get; } = CreateMesh();
    private static readonly MaterialData[] Wrappings =
    [
        Wrapping(new(208, 47, 72), new(255, 215, 112)),
        Wrapping(new(40, 130, 199), new(245, 249, 255)),
        Wrapping(new(43, 158, 124), new(255, 211, 106)),
        Wrapping(new(139, 76, 186), new(255, 222, 163)),
    ];

    public static MaterialData MaterialFor(int index) => Wrappings[index % Wrappings.Length];

    private static MaterialData Wrapping(Vector3 paper, Vector3 ribbon)
    {
        const int tile = 64, width = tile * 3;
        var pixels = new byte[width * tile * 4];
        for (int y = 0; y < tile; y++)
        for (int x = 0; x < width; x++)
        {
            int region = x / tile, local = x % tile;
            bool dot = (local % 16 - 8) * (local % 16 - 8) + (y % 16 - 8) * (y % 16 - 8) <= 3;
            Vector3 color = region == 1 ? ribbon : paper * (region == 2 ? 1.08f : 1);
            if (region != 1 && dot) color = Vector3.Lerp(color, ribbon, 0.45f);
            int pixel = (y * width + x) * 4;
            pixels[pixel] = (byte)Math.Clamp(color.X, 0, 255);
            pixels[pixel + 1] = (byte)Math.Clamp(color.Y, 0, 255);
            pixels[pixel + 2] = (byte)Math.Clamp(color.Z, 0, 255);
            pixels[pixel + 3] = 255;
        }
        return new MaterialData { Texture = new TextureData(width, tile, pixels) };
    }

    private static MeshData CreateMesh()
    {
        var vertices = new List<Vertex>();
        var indices = new List<uint>();
        var rounded = RoundedBox();
        Box(new(0, -0.12f, 0), new(1.45f, 1.15f, 1.45f), 0, rounded);
        Box(new(0, 0.48f, 0), new(1.56f, 0.18f, 1.56f), 2, rounded);
        foreach (int side in new[] { -1, 1 })
        {
            Box(new(0, -0.12f, side * 0.733f), new(0.22f, 1.1f, 0.025f), 1);
            Box(new(side * 0.733f, -0.12f, 0), new(0.025f, 1.1f, 0.22f), 1);
            Box(new(0, 0.48f, side * 0.786f), new(0.22f, 0.17f, 0.025f), 1);
            Box(new(side * 0.786f, 0.48f, 0), new(0.025f, 0.17f, 0.22f), 1);
            Bow(side);
        }
        Box(new(0, 0.581f, 0), new(0.22f, 0.025f, 1.54f), 1);
        Box(new(0, 0.581f, 0), new(1.54f, 0.025f, 0.22f), 1);
        Box(new(0, 0.67f, 0), new(0.28f, 0.2f, 0.25f), 1, rounded);
        // Физический радиус и расстояние сбора остаются прежними.
        float radius = vertices.Max(vertex => vertex.Position.Length());
        return new MeshData(vertices.Select(vertex => vertex with { Position = vertex.Position / radius }).ToArray(), indices);

        void Box(Vector3 center, Vector3 size, int tile, MeshData? source = null)
        {
            source ??= GameMeshes.Box;
            uint first = (uint)vertices.Count;
            foreach (var vertex in source.Vertices)
                vertices.Add(new(center + vertex.Position * size, Uv(vertex.Uv, tile),
                    Vector3.Normalize(vertex.Normal / size)));
            foreach (uint index in source.Indices) indices.Add(first + index);
        }

        void Bow(int side)
        {
            const int segments = 24;
            for (int i = 0; i < segments; i++)
            {
                Vector3 a = Point(i), b = Point(i + 1);
                Vector3 normal = Vector3.Normalize(Vector3.Cross(b - a, Vector3.UnitZ));
                Vector3 width = Vector3.UnitZ * 0.09f, thickness = normal * 0.008f;
                Quad(a - width + thickness, b - width + thickness, b + width + thickness, a + width + thickness);
                Quad(a + width - thickness, b + width - thickness, b - width - thickness, a - width - thickness);
                Quad(a - width - thickness, b - width - thickness, b - width + thickness, a - width + thickness);
                Quad(a + width + thickness, b + width + thickness, b + width - thickness, a + width - thickness);
            }
            Vector3 Point(int index)
            {
                float angle = index * MathF.Tau / segments;
                return new(side * 0.29f * (1 - MathF.Cos(angle)),
                    0.66f + 0.18f * MathF.Sin(angle) + 0.11f * (1 - MathF.Cos(angle)), 0);
            }
        }

        void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            uint first = (uint)vertices.Count;
            Vector3 normal = Vector3.Normalize(Vector3.Cross(b - a, c - a));
            vertices.Add(new(a, Uv(Vector2.Zero, 1), normal));
            vertices.Add(new(b, Uv(Vector2.UnitX, 1), normal));
            vertices.Add(new(c, Uv(Vector2.One, 1), normal));
            vertices.Add(new(d, Uv(Vector2.UnitY, 1), normal));
            indices.AddRange([first, first + 1, first + 2, first, first + 2, first + 3]);
        }
    }

    private static Vector2 Uv(Vector2 value, int tile) => new((tile + 0.03f + value.X * 0.94f) / 3, 0.03f + value.Y * 0.94f);

    private static MeshData RoundedBox()
    {
        float[] coordinates = [-0.5f, -0.4f, 0.4f, 0.5f];
        (Vector3 Normal, Vector3 U, Vector3 V)[] faces =
        [
            (Vector3.UnitX, -Vector3.UnitZ, Vector3.UnitY), (-Vector3.UnitX, Vector3.UnitZ, Vector3.UnitY),
            (Vector3.UnitY, Vector3.UnitX, -Vector3.UnitZ), (-Vector3.UnitY, Vector3.UnitX, Vector3.UnitZ),
            (Vector3.UnitZ, Vector3.UnitX, Vector3.UnitY), (-Vector3.UnitZ, -Vector3.UnitX, Vector3.UnitY),
        ];
        var vertices = new List<Vertex>();
        var indices = new List<uint>();
        foreach (var face in faces)
        {
            uint first = (uint)vertices.Count;
            foreach (float y in coordinates)
            foreach (float x in coordinates)
            {
                Vector3 point = face.Normal * 0.5f + face.U * x + face.V * y;
                Vector3 core = Vector3.Clamp(point, new(-0.4f), new(0.4f));
                Vector3 normal = Vector3.Normalize(point - core);
                vertices.Add(new(core + normal * 0.1f, new(x + 0.5f, y + 0.5f), normal));
            }
            for (uint y = 0; y < 3; y++)
            for (uint x = 0; x < 3; x++)
            {
                uint a = first + y * 4 + x, b = a + 4;
                indices.AddRange([a, a + 1, b + 1, a, b + 1, b]);
            }
        }
        return new MeshData(vertices, indices);
    }
}
