using System.Numerics;
using Engine3D.Core;

namespace Platformer.App;

/// <summary>Широкий асимметричный хребет; снег и скала делят одну поверхность без наложений.</summary>
internal static class WinterMountain
{
    internal static (MeshData Rock, MeshData Snow) Create()
    {
        var rock = new List<Vertex>(); var snow = new List<Vertex>();
        const int divisions = 20;
        Vector3 Point(int x, int z)
        {
            float u = x * 2f / divisions - 1, v = z * 2f / divisions - 1;
            float Peak(float cx, float cz, float height, float sx, float sz) =>
                height * MathF.Exp(-((u - cx) * (u - cx) / sx + (v - cz) * (v - cz) / sz));
            float ridge = MathF.Max(Peak(-0.38f, -0.1f, 0.84f, 0.13f, 0.24f),
                MathF.Max(Peak(0.12f, 0.08f, 1, 0.12f, 0.18f), Peak(0.5f, -0.18f, 0.66f, 0.1f, 0.21f)));
            float edge = Math.Clamp((1 - MathF.Max(MathF.Abs(u), MathF.Abs(v))) * 5, 0, 1);
            float folds = 1 + 0.065f * MathF.Sin(u * 21 + v * 13) + 0.035f * MathF.Cos(v * 27 - u * 9);
            // Круглый нерегулярный подножный контур вместо квадратного среза height-map.
            float outline = 1 + 0.055f * MathF.Sin(u * 8 + v * 5);
            return new(u * MathF.Sqrt(1 - v * v * 0.5f) * outline, ridge * edge * folds,
                v * MathF.Sqrt(1 - u * u * 0.5f) * outline);
        }
        for (int z = 0; z < divisions; z++)
        for (int x = 0; x < divisions; x++)
        {
            Vector3 a = Point(x, z), b = Point(x + 1, z), c = Point(x, z + 1), d = Point(x + 1, z + 1);
            Split(a, c, b); Split(b, c, d);
        }
        // Замкнутое основание: при обзоре сверху/сбоку хребет не выглядит вырезанной плоскостью.
        for (int i = 0; i < divisions; i++)
        {
            Side(Point(i, 0), Point(i + 1, 0)); Side(Point(divisions, i), Point(divisions, i + 1));
            Side(Point(i + 1, divisions), Point(i, divisions)); Side(Point(0, i + 1), Point(0, i));
        }
        return (Mesh(rock), Mesh(snow));

        void Side(Vector3 a, Vector3 b)
        {
            Emit(rock, a, b, new(0.08f, -0.85f, -0.06f));
        }
        void Split(Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3[] triangle = [a, b, c];
            Clip(rock, triangle, false); Clip(snow, triangle, true);
        }
        void Clip(List<Vertex> target, Vector3[] triangle, bool above)
        {
            // Неровная снеговая линия, одинаковая для обоих материалов.
            float Distance(Vector3 p) => p.Y - (0.54f + p.X * 0.065f - p.Z * 0.045f);
            var polygon = new List<Vector3>();
            for (int i = 0; i < 3; i++)
            {
                Vector3 a = triangle[i], b = triangle[(i + 1) % 3];
                float da = Distance(a), db = Distance(b);
                bool insideA = above ? da >= 0 : da <= 0, insideB = above ? db >= 0 : db <= 0;
                if (insideA) polygon.Add(a);
                if (insideA != insideB) polygon.Add(Vector3.Lerp(a, b, da / (da - db)));
            }
            for (int i = 1; i + 1 < polygon.Count; i++) Emit(target, polygon[0], polygon[i], polygon[i + 1]);
        }
        static void Emit(List<Vertex> target, Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 cross = Vector3.Cross(b - a, c - a);
            if (cross.LengthSquared() < 1e-12f) return;
            Vector3 normal = Vector3.Normalize(cross);
            foreach (var p in new[] { a, b, c }) target.Add(new(p, new((p.X + 1) * 0.5f, p.Y), normal));
        }
        static MeshData Mesh(List<Vertex> vertices) => new(vertices.ToArray(), Enumerable.Range(0, vertices.Count).Select(i => (uint)i).ToArray());
    }
}
