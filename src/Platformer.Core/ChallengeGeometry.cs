using System.Numerics;

namespace Platformer.Core;

/// <summary>Общие примитивы пяти авторских маршрутов, без генерации случайной геометрии.</summary>
internal static class ChallengeGeometry
{
    internal static SurfaceDefinition Flat(string id, float x, float top, float z,
        float width, float depth, float collapse = 0) =>
        new(id, new(new(x, top - 0.2f, z), new(width / 2, 0.2f, depth / 2), Quaternion.Identity), collapse);

    internal static SurfaceDefinition Between(string id, Vector3 from, Vector3 to, float width)
    {
        Vector3 x = Vector3.Normalize(to - from);
        Vector3 y = Vector3.Normalize(Vector3.UnitY - x * x.Y);
        Vector3 z = Vector3.Cross(x, y);
        var rotation = Quaternion.CreateFromRotationMatrix(new Matrix4x4(
            x.X, x.Y, x.Z, 0, y.X, y.Y, y.Z, 0, z.X, z.Y, z.Z, 0, 0, 0, 0, 1));
        float overlap = MathF.Abs(x.Y) < 0.001f ? 0.03f : 0;
        return new(id, new((from + to) / 2 - y * 0.2f,
            new(Vector3.Distance(from, to) / 2 + overlap, 0.2f, width / 2), rotation));
    }

    internal static CoinDefinition[] Coins(params Vector3[] points) => points.Select((point, index) =>
        new CoinDefinition($"coin-{index + 1:00}", point + Vector3.UnitY * 0.8f, 0.2f)).ToArray();

    internal static OrientedBox Finish(Vector3 floor, bool alongX) =>
        new(floor + Vector3.UnitY * 0.75f, new(1.2f, 0.75f, 0.12f),
            alongX ? Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 2) : Quaternion.Identity);

    internal static HazardDefinition Bumper(string id, Vector3 floor, float reach, float speed, float phase = 0) =>
        new(id, floor + Vector3.UnitY * 0.55f, Vector3.UnitY, Vector3.Zero,
            new(reach, 0.22f, 0.22f), Quaternion.Identity, speed, phase, HazardResponse.Knockback);

    internal static HazardDefinition Hurdle(string id, Vector3 floor, float width) =>
        new(id, floor + Vector3.UnitY * 0.25f, Vector3.UnitY, Vector3.Zero,
            new(0.1f, 0.25f, width / 2), Quaternion.Identity, 0, 0);
}
