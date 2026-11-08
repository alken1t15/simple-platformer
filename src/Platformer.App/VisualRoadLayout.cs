using System.Collections.Concurrent;
using System.Globalization;
using System.Numerics;
using System.Text;
using Engine3D.Core;
using Platformer.Core;

namespace Platformer.App;

/// <summary>Общая сетка плиток и разбиение пересечений без копланарных дублей.</summary>
internal sealed class VisualRoadLayout
{
    public const float Pitch = 0.65f;
    private static readonly ConcurrentDictionary<string, VisualRoadLayout> Cache = new();
    public IReadOnlyList<Patch> Patches { get; }

    public sealed class Patch
    {
        public OrientedBox Bounds { get; }
        public Vector3 Normal { get; }
        public Vector3 U { get; }
        public Vector3 V { get; }
        public float Height { get; }
        public Vector2[] Polygon { get; }
        public Vector2[] OuterPolygon { get; }
        public MeshData? Top { get; set; }
        public Dictionary<(int X, int Z), MeshData> Shadows { get; } = new();

        public Patch(OrientedBox bounds)
        {
            Bounds = bounds;
            Normal = Vector3.Normalize(Vector3.Transform(Vector3.UnitY, bounds.Orientation));
            Vector3 axis = MathF.Abs(Normal.X) < 0.95f ? Vector3.UnitX : Vector3.UnitZ;
            U = Vector3.Normalize(axis - Normal * Vector3.Dot(axis, Normal));
            V = Vector3.Cross(U, Normal);
            Height = Vector3.Dot(Normal, bounds.ToWorld(new(0, bounds.HalfExtents.Y, 0)));
            OuterPolygon = new[]
            {
                Project(bounds.ToWorld(new(-bounds.HalfExtents.X, bounds.HalfExtents.Y, -bounds.HalfExtents.Z))),
                Project(bounds.ToWorld(new(bounds.HalfExtents.X, bounds.HalfExtents.Y, -bounds.HalfExtents.Z))),
                Project(bounds.ToWorld(new(bounds.HalfExtents.X, bounds.HalfExtents.Y, bounds.HalfExtents.Z))),
                Project(bounds.ToWorld(new(-bounds.HalfExtents.X, bounds.HalfExtents.Y, bounds.HalfExtents.Z))),
            };
            if (Area(OuterPolygon) < 0) Array.Reverse(OuterPolygon);
            float x = bounds.HalfExtents.X - 0.09f, z = bounds.HalfExtents.Z - 0.09f;
            Polygon = x <= 0 || z <= 0 ? [] : new[]
            {
                Project(bounds.ToWorld(new(-x, bounds.HalfExtents.Y, -z))),
                Project(bounds.ToWorld(new(x, bounds.HalfExtents.Y, -z))),
                Project(bounds.ToWorld(new(x, bounds.HalfExtents.Y, z))),
                Project(bounds.ToWorld(new(-x, bounds.HalfExtents.Y, z))),
            };
            if (Area(Polygon) < 0) Array.Reverse(Polygon);
        }

        public Vector2 Project(Vector3 p) => new(Vector3.Dot(p, U), Vector3.Dot(p, V));
        public Vector3 World(Vector2 p, float offset = 0) => U * p.X + V * p.Y + Normal * (Height + offset);
        public bool Contains(Vector2 p)
        {
            if (Polygon.Length == 0) return false;
            for (int i = 0; i < Polygon.Length; i++)
                if (Cross(Polygon[(i + 1) % Polygon.Length] - Polygon[i], p - Polygon[i]) < -1e-6f) return false;
            return true;
        }
    }

    public static VisualRoadLayout Get(IReadOnlyList<SurfaceDefinition> surfaces)
    {
        var key = new StringBuilder();
        foreach (var surface in surfaces)
        {
            var b = surface.Bounds;
            foreach (float value in new[] { b.Center.X, b.Center.Y, b.Center.Z, b.HalfExtents.X, b.HalfExtents.Y,
                b.HalfExtents.Z, b.Orientation.X, b.Orientation.Y, b.Orientation.Z, b.Orientation.W })
                key.Append(BitConverter.SingleToInt32Bits(value).ToString("X8", CultureInfo.InvariantCulture));
        }
        return Cache.GetOrAdd(key.ToString(), _ => new(surfaces));
    }

    private VisualRoadLayout(IReadOnlyList<SurfaceDefinition> surfaces)
    {
        var patches = surfaces.Select(s => new Patch(s.Bounds)).ToArray();
        Patches = Array.AsReadOnly(patches);
        for (int index = 0; index < patches.Length; index++)
        {
            var patch = patches[index];
            var pieces = new List<List<Vector2>> { patch.OuterPolygon.ToList() };
            foreach (var earlier in patches.Take(index))
            {
                if (Vector3.Dot(patch.Normal, earlier.Normal) < 0.999999f
                    || MathF.Abs(patch.Height - earlier.Height) > 1e-5f) continue;
                var cover = earlier.OuterPolygon.Select(p => patch.Project(earlier.World(p))).ToArray();
                pieces = pieces.SelectMany(p => Subtract(p, cover)).ToList();
            }
            var vertices = new List<Vertex>();
            var indices = new List<uint>();
            foreach (var piece in pieces)
            {
                var inset = patch.Polygon.Length == 0 ? [] : Clip(piece, patch.Polygon);
                IEnumerable<List<Vector2>> rim = patch.Polygon.Length == 0 ? [piece] : Subtract(piece, patch.Polygon);
                foreach (var border in rim)
                    AddPolygon(border, patch, 0.002f, _ => new(0.75f, 0.5f), vertices, indices);
                if (inset.Count < 3) continue;
                var range = CellRange(inset);
                for (int z = range.MinZ; z <= range.MaxZ; z++)
                for (int x = range.MinX; x <= range.MaxX; x++)
                {
                    var tile = Clip(inset, Rectangle(x * Pitch, z * Pitch, (x + 1) * Pitch, (z + 1) * Pitch));
                    AddPolygon(tile, patch, 0.002f, p => new(
                        (((x + z) & 1) == 0 ? 0.01f : 0.51f) + Math.Clamp(p.X / Pitch - x, 0, 1) * 0.48f,
                        0.98f - Math.Clamp(p.Y / Pitch - z, 0, 1) * 0.96f), vertices, indices);
                }
            }
            patch.Top = vertices.Count == 0 ? null : new(vertices, indices);

            // Ограниченный набор обрезанных контактных мешей создаётся до игрового цикла.
            if (patch.Polygon.Length == 0) continue;
            var cells = CellRange(patch.Polygon);
            for (int z = cells.MinZ; z <= cells.MaxZ; z++)
            for (int x = cells.MinX; x <= cells.MaxX; x++)
            {
                if (MathF.Abs(Area(Clip(patch.Polygon, Rectangle(x * Pitch, z * Pitch, (x + 1) * Pitch, (z + 1) * Pitch)))) < 1e-8f) continue;
                float left = (x - 1) * Pitch, near = (z - 1) * Pitch;
                var footprint = Clip(patch.Polygon, Rectangle(left, near, left + 3 * Pitch, near + 3 * Pitch));
                var shadowVertices = new List<Vertex>(); var shadowIndices = new List<uint>();
                AddPolygon(footprint, patch, 0.004f, p => new((p.X - left) / (3 * Pitch), 1 - (p.Y - near) / (3 * Pitch)),
                    shadowVertices, shadowIndices);
                if (shadowVertices.Count > 0) patch.Shadows.Add((x, z), new(shadowVertices, shadowIndices));
            }
        }
    }

    private static void AddPolygon(IReadOnlyList<Vector2> polygon, Patch patch, float offset,
        Func<Vector2, Vector2> uv, List<Vertex> vertices, List<uint> indices)
    {
        for (int i = 1; i + 1 < polygon.Count; i++)
        {
            if (MathF.Abs(Cross(polygon[i] - polygon[0], polygon[i + 1] - polygon[0])) < 1e-8f) continue;
            uint first = (uint)vertices.Count;
            foreach (int j in new[] { 0, i + 1, i })
                vertices.Add(new(patch.World(polygon[j], offset), uv(polygon[j]), patch.Normal));
            indices.AddRange([first, first + 1, first + 2]);
        }
    }

    private static IEnumerable<List<Vector2>> Subtract(IReadOnlyList<Vector2> subject, IReadOnlyList<Vector2> cover)
    {
        var remaining = subject.ToList();
        for (int i = 0; i < cover.Count && remaining.Count >= 3; i++)
        {
            var outside = HalfPlane(remaining, cover[i], cover[(i + 1) % cover.Count], inside: false);
            if (MathF.Abs(Area(outside)) > 1e-8f) yield return outside;
            remaining = HalfPlane(remaining, cover[i], cover[(i + 1) % cover.Count], inside: true);
        }
    }

    private static List<Vector2> Clip(IReadOnlyList<Vector2> polygon, IReadOnlyList<Vector2> cover)
    {
        var result = polygon.ToList();
        for (int i = 0; i < cover.Count && result.Count > 0; i++)
            result = HalfPlane(result, cover[i], cover[(i + 1) % cover.Count], inside: true);
        return result;
    }

    private static List<Vector2> HalfPlane(IReadOnlyList<Vector2> polygon, Vector2 a, Vector2 b, bool inside)
    {
        var result = new List<Vector2>();
        if (polygon.Count == 0) return result;
        Vector2 previous = polygon[^1];
        float dp = Cross(b - a, previous - a);
        foreach (var current in polygon)
        {
            float dc = Cross(b - a, current - a);
            bool keepCurrent = inside ? dc >= 0 : dc <= 0, keepPrevious = inside ? dp >= 0 : dp <= 0;
            if (keepCurrent != keepPrevious) result.Add(Vector2.Lerp(previous, current, dp / (dp - dc)));
            if (keepCurrent) result.Add(current);
            previous = current; dp = dc;
        }
        return result;
    }

    private static Vector2[] Rectangle(float left, float near, float right, float far) =>
        [new(left, near), new(right, near), new(right, far), new(left, far)];
    private static (int MinX, int MinZ, int MaxX, int MaxZ) CellRange(IReadOnlyList<Vector2> points) =>
        ((int)MathF.Floor(points.Min(p => p.X) / Pitch), (int)MathF.Floor(points.Min(p => p.Y) / Pitch),
         (int)MathF.Floor(points.Max(p => p.X) / Pitch), (int)MathF.Floor(points.Max(p => p.Y) / Pitch));
    private static float Area(IReadOnlyList<Vector2> p)
    {
        float area = 0;
        for (int i = 0; i < p.Count; i++) area += Cross(p[i], p[(i + 1) % p.Count]);
        return area / 2;
    }
    private static float Cross(Vector2 a, Vector2 b) => a.X * b.Y - a.Y * b.X;
}
