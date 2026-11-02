using System.Numerics;

namespace Platformer.Core;

/// <summary>Неизменяемый OBB: половины размеров и поворот из local в world, Y вверх.</summary>
public sealed class OrientedBox
{
    public Vector3 Center { get; }
    public Vector3 HalfExtents { get; }
    public Quaternion Orientation { get; }

    public OrientedBox(Vector3 center, Vector3 halfExtents, Quaternion orientation)
    {
        LevelValidation.Position(center, nameof(center));
        LevelValidation.Extents(halfExtents, nameof(halfExtents));
        Center = center;
        HalfExtents = halfExtents;
        Orientation = LevelValidation.Rotation(orientation, nameof(orientation));
    }

    public Vector3 ToLocal(Vector3 point)
    {
        LevelValidation.Finite(point, nameof(point));
        return Vector3.Transform(point - Center, Quaternion.Conjugate(Orientation));
    }

    public Vector3 ToWorld(Vector3 point)
    {
        LevelValidation.Finite(point, nameof(point));
        return Center + Vector3.Transform(point, Orientation);
    }

    /// <summary>Для внутренней точки возвращается сама точка, для внешней — точка объёма.</summary>
    public Vector3 ClosestPoint(Vector3 point) =>
        ToWorld(Vector3.Clamp(ToLocal(point), -HalfExtents, HalfExtents));
}
