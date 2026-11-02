using System.Numerics;
using Engine3D.Core;

namespace Platformer.App;

/// <summary>Общие immutable CPU-меши: renderer кеширует каждый экземпляр один раз.</summary>
public static class GameMeshes
{
    public static MeshData Sphere { get; } = CreateSphere(24, 16);
    public static MeshData Box { get; } = PrimitiveFactory.CreateCube();
    public static MeshData Coin { get; } = CreateCoin();

    /// <summary>Единичный радиус, ось Y, CCW снаружи; отдельные UV на шве и полюсах.</summary>
    public static MeshData CreateSphere(int segments, int rings)
    {
        if (segments is < 3 or > 256) throw new ArgumentOutOfRangeException(nameof(segments));
        if (rings is < 2 or > 256) throw new ArgumentOutOfRangeException(nameof(rings));
        int stride = segments + 1;
        var vertices = new Vertex[(rings + 1) * stride];
        var indices = new List<uint>(6 * segments * (rings - 1));
        for (int row = 0; row <= rings; row++)
        {
            float latitude = row * MathF.PI / rings;
            float y = row == 0 ? 1f : row == rings ? -1f : MathF.Cos(latitude);
            float radius = row == 0 || row == rings ? 0f : MathF.Sin(latitude);
            for (int column = 0; column <= segments; column++)
            {
                float longitude = column == segments ? 0f : column * MathF.Tau / segments;
                var position = new Vector3(radius * MathF.Cos(longitude), y, radius * MathF.Sin(longitude));
                vertices[row * stride + column] = new Vertex(position,
                    new Vector2((float)column / segments, 1f - (float)row / rings), position);
            }
        }
        for (int row = 0; row < rings; row++)
        for (int column = 0; column < segments; column++)
        {
            uint a = (uint)(row * stride + column);
            uint b = a + (uint)stride;
            if (row > 0) indices.AddRange([a, a + 1, b]);
            if (row < rings - 1) indices.AddRange([a + 1, b + 1, b]);
        }
        return new MeshData(vertices, indices);
    }

    // Короткий цилиндр по Z целиком внутри единичной сферы триггера монеты
    private static MeshData CreateCoin()
    {
        const int segments = 24;
        var vertices = new List<Vertex>();
        var indices = new List<uint>();
        for (int index = 0; index < segments; index++)
        {
            float a = index * MathF.Tau / segments;
            float b = (index + 1) * MathF.Tau / segments;
            Vector3 p = new(0.98f * MathF.Cos(a), 0.98f * MathF.Sin(a), 0f);
            Vector3 q = new(0.98f * MathF.Cos(b), 0.98f * MathF.Sin(b), 0f);
            Vector3 front = new(0f, 0f, 0.1f);
            Triangle(front, p + front, q + front, Vector3.UnitZ);
            Triangle(-front, q - front, p - front, -Vector3.UnitZ);
            var normalP = Vector3.Normalize(p);
            var normalQ = Vector3.Normalize(q);
            uint first = (uint)vertices.Count;
            vertices.Add(new(p - front, new(0f, 0f), normalP));
            vertices.Add(new(q - front, new(1f, 0f), normalQ));
            vertices.Add(new(q + front, new(1f, 1f), normalQ));
            vertices.Add(new(p + front, new(0f, 1f), normalP));
            indices.AddRange([first, first + 1, first + 2, first, first + 2, first + 3]);
        }
        return new MeshData(vertices, indices);

        void Triangle(Vector3 a, Vector3 b, Vector3 c, Vector3 normal)
        {
            uint first = (uint)vertices.Count;
            vertices.Add(new(a, new(0.5f, 0.5f), normal));
            vertices.Add(new(b, new(0f, 0f), normal));
            vertices.Add(new(c, new(1f, 1f), normal));
            indices.AddRange([first, first + 1, first + 2]);
        }
    }
}
