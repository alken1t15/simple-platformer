using System.Numerics;
using Engine3D.Core;

namespace Platformer.App;

internal sealed partial class VisualScene
{
    private readonly List<(Vector3 Position, float X, float Z)> _sites = [];
    private readonly List<(SceneObject[] Objects, Vector3[] Origins, float Phase)> _balloons = [];
    private static readonly MeshData Tower = Lathe([(0.5f, 0), (0.5f, 0.05f), (0.46f, 0.09f), (0.34f, 0.94f), (0.43f, 1)]);
    private static readonly MeshData Cone = Lathe([(0.58f, 0), (0.6f, 0.08f), (0.07f, 0.9f), (0, 1)]);
    private static readonly MeshData Balloon = Lathe([(0.15f, -1.1f), (0.18f, -0.95f), (0.4f, -0.72f),
        (0.7f, -0.35f), (0.86f, 0.12f), (0.8f, 0.55f), (0.56f, 0.9f), (0.25f, 1.08f), (0, 1.12f)]);
    private static readonly MeshData Ring = CreateRing();
    private static readonly MaterialData TowerPaint = Textured(Texture(32, 128, (_, y) =>
        (y / 25 & 1) == 0 ? new(255, 242, 211) : new(241, 108, 105)));
    private static readonly MaterialData Brass = Solid(255, 211, 90);
    private static readonly MaterialData Turquoise = Solid(35, 173, 174);
    private static readonly MaterialData CoralBalloon = BalloonPaint(new(249, 125, 116));
    private static readonly MaterialData BlueBalloon = BalloonPaint(new(58, 177, 213));

    private static MaterialData BalloonPaint(Vector3 color) => Textured(Texture(128, 64, (x, y) =>
    {
        float shade = x % 16 < 1 ? 0.78f : 0.92f + 0.08f * MathF.Sin(x % 16 / 15f * MathF.PI);
        return ((x / 16 & 1) == 0 ? color : new Vector3(255, 239, 191)) * shade;
    }));

    private static float PlantScale(float x, float z) => Math.Clamp(MathF.Min(x, z) / 3.2f, 0.4f, 1);

    private bool TryDecorationSite(Vector3 requested, float rx, float rz, int rings, out Vector3 result)
    {
        for (int ring = 0; ring <= rings; ring++)
        for (int side = 0; side < (ring == 0 ? 1 : 16); side++)
        {
            float angle = side * MathF.Tau / 16;
            Vector3 candidate = requested + new Vector3(MathF.Cos(angle), 0, MathF.Sin(angle)) * (ring * 1.25f);
            if (!ClearOfRoads(candidate, rx, rz)) continue;
            if (_sites.Any(s => new Vector2((candidate.X - s.Position.X) / (rx + s.X),
                (candidate.Z - s.Position.Z) / (rz + s.Z)).LengthSquared() < 1)) continue;
            _sites.Add((candidate, rx, rz));
            result = candidate;
            return true;
        }
        result = requested;
        return false;
    }

    private bool ClearOfRoads(Vector3 center, float rx, float rz)
    {
        foreach (var patch in _roads.Patches)
        {
            var b = patch.Bounds;
            Vector2[] points = new[] { new Vector3(-b.HalfExtents.X, 0, -b.HalfExtents.Z),
                new(b.HalfExtents.X, 0, -b.HalfExtents.Z), new(b.HalfExtents.X, 0, b.HalfExtents.Z),
                new(-b.HalfExtents.X, 0, b.HalfExtents.Z) }.Select(p =>
                {
                    Vector3 world = b.ToWorld(p);
                    return new Vector2((world.X - center.X) / rx, (world.Z - center.Z) / rz);
                }).ToArray();
            bool positive = true, negative = true;
            float distance = float.PositiveInfinity;
            for (int i = 0; i < points.Length; i++)
            {
                Vector2 p = points[i], q = points[(i + 1) % points.Length], edge = q - p;
                float cross = edge.X * -p.Y - edge.Y * -p.X;
                positive &= cross >= 0; negative &= cross <= 0;
                float length = edge.LengthSquared();
                float t = length == 0 ? 0 : Math.Clamp(-Vector2.Dot(p, edge) / length, 0, 1);
                distance = MathF.Min(distance, (p + edge * t).LengthSquared());
            }
            if (positive || negative || distance < 1) return false;
        }
        return true;
    }

    private void AddLandmarks()
    {
        int scattered = 0;
        for (int i = 0; i < _roads.Patches.Count && scattered < 8; i += 2)
        {
            var b = _roads.Patches[i].Bounds;
            Vector3 axis = Vector3.Transform(b.HalfExtents.X > b.HalfExtents.Z ? Vector3.UnitZ : Vector3.UnitX, b.Orientation);
            float half = MathF.Min(b.HalfExtents.X, b.HalfExtents.Z);
            Vector3 requested = b.Center + axis * ((half + 4) * (i % 4 == 0 ? -1 : 1));
            requested.Y = b.Center.Y - 1.1f;
            if (!TryDecorationSite(requested, 2.9f, 2.9f, 5, out var site)) continue;
            IslandAt(site, 1.6f, 1.4f, 1, i * 0.7f);
            scattered++;
        }
        Vector2 middle = (_routeMin + _routeMax) / 2;
        if (TryDecorationSite(new(middle.X + 5, -1.2f, middle.Y + 3), 3.5f, 3.5f, 16, out var lighthouse))
        {
            IslandAt(lighthouse, 2.5f, 2.3f, 0, 0);
            LighthouseAt(lighthouse + new Vector3(0, 0.06f, 0));
        }
        BalloonAt(new(_routeMin.X - 7, 10, middle.Y - 4), 1.4f, CoralBalloon);
        BalloonAt(new(_routeMax.X + 5, 14, middle.Y + 10), 1.7f, BlueBalloon);
        BalloonAt(new(middle.X + 4, 7.8f, _routeMin.Y - 18), 1.3f, CoralBalloon);
    }

    private void LighthouseAt(Vector3 foot)
    {
        int first = _scene.Objects.Count;
        Add(SoftBox, Sand, foot, new(1.65f, 0.13f, 1.65f));
        Add(Tower, TowerPaint, foot + new Vector3(0, 0.06f, 0), new(1.45f, 3.5f, 1.45f));
        Add(Ring, Turquoise, foot + new Vector3(0, 3.53f, 0), new(1.5f));
        Add(Cloud, Brass, foot + new Vector3(0, 3.86f, 0), new(0.29f, 0.36f, 0.29f));
        for (int i = 0; i < 4; i++)
        {
            float angle = i * MathF.PI / 2 + MathF.PI / 4;
            Add(SoftBox, Turquoise, foot + new Vector3(MathF.Cos(angle) * 0.43f, 3.9f, MathF.Sin(angle) * 0.43f),
                new(0.055f, 0.72f, 0.055f));
        }
        Add(Ring, Turquoise, foot + new Vector3(0, 4.25f, 0), new(1.05f));
        Add(Cone, Turquoise, foot + new Vector3(0, 4.22f, 0), new(1.15f, 0.65f, 1.15f));
        Add(Cloud, Brass, foot + new Vector3(0, 4.9f, 0), new(0.08f));
        ApplyDistancePalette(first, foot);
    }

    private void BalloonAt(Vector3 center, float scale, MaterialData material)
    {
        int first = _scene.Objects.Count;
        Add(Balloon, material, center, new(scale));
        Add(SoftBox, Timber, center - new Vector3(0, scale * 1.68f, 0), new Vector3(0.45f, 0.3f, 0.4f) * scale);
        Add(Ring, Brass, center - new Vector3(0, scale * 1.52f, 0), new Vector3(0.48f, 0.4f, 0.42f) * scale);
        foreach (int x in new[] { -1, 1 }) foreach (int z in new[] { -1, 1 })
            Add(SoftBox, Timber, center + new Vector3(x * 0.15f, -1.32f, z * 0.13f) * scale,
                new Vector3(0.018f, 0.47f, 0.018f) * scale);
        var objects = _scene.Objects.Skip(first).ToArray();
        _balloons.Add((objects, objects.Select(o => o.Transform.Position).ToArray(), _balloons.Count * 1.7f));
    }

    private void AnimateLandmarks(double seconds)
    {
        foreach (var balloon in _balloons)
        {
            float phase = (float)Math.IEEERemainder(seconds * 0.35, Math.Tau) + balloon.Phase;
            float verticalPhase = (float)Math.IEEERemainder(seconds * 0.28, Math.Tau) + balloon.Phase * 0.8f;
            Vector3 offset = new(MathF.Sin(phase) * 0.24f, MathF.Sin(verticalPhase) * 0.18f, MathF.Cos(phase) * 0.16f);
            for (int i = 0; i < balloon.Objects.Length; i++) balloon.Objects[i].Transform.Position = balloon.Origins[i] + offset;
        }
    }

    private static MeshData Lathe((float Radius, float Y)[] profile)
    {
        const int sides = 40;
        var mesh = new MeshBuilder();
        float lo = profile.Min(p => p.Y), span = profile.Max(p => p.Y) - lo;
        for (int ring = 0; ring < profile.Length - 1; ring++)
        for (int i = 0; i < sides; i++)
        {
            Vector3 a = Point(ring, i), b = Point(ring, i + 1), c = Point(ring + 1, i), d = Point(ring + 1, i + 1);
            mesh.Triangle(a, c, b, new((float)i / sides, (a.Y - lo) / span),
                new((float)i / sides, (c.Y - lo) / span), new((float)(i + 1) / sides, (b.Y - lo) / span));
            mesh.Triangle(b, c, d, new((float)(i + 1) / sides, (b.Y - lo) / span),
                new((float)i / sides, (c.Y - lo) / span), new((float)(i + 1) / sides, (d.Y - lo) / span));
        }
        return mesh.Build(smooth: true);
        Vector3 Point(int ring, int i)
        {
            float angle = i % sides * MathF.Tau / sides;
            return new(MathF.Cos(angle) * profile[ring].Radius, profile[ring].Y, MathF.Sin(angle) * profile[ring].Radius);
        }
    }

    private static MeshData CreateRing()
    {
        var mesh = new MeshBuilder();
        for (int i = 0; i < 32; i++) for (int j = 0; j < 8; j++)
        {
            var a = Point(i, j); var b = Point(i + 1, j); var c = Point(i + 1, j + 1); var d = Point(i, j + 1);
            mesh.Triangle(a, c, b); mesh.Triangle(a, d, c);
        }
        return mesh.Build(smooth: true);
        static Vector3 Point(int i, int j)
        {
            float a = i % 32 * MathF.Tau / 32, b = j % 8 * MathF.Tau / 8;
            float r = 0.5f + MathF.Cos(b) * 0.035f;
            return new(MathF.Cos(a) * r, MathF.Sin(b) * 0.035f, MathF.Sin(a) * r);
        }
    }
}
