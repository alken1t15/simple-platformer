using System.Numerics;

namespace Platformer.Core;

public enum HazardResponse { Fail, Knockback }

/// <summary>Данные вращающейся балки, без вычисления движения или столкновений.</summary>
public sealed class HazardDefinition
{
    public string Id { get; }
    public Vector3 Pivot { get; }
    public Vector3 Axis { get; }
    public Vector3 CenterOffset { get; }
    public Vector3 HalfExtents { get; }
    public Quaternion InitialOrientation { get; }
    public float AngularSpeed { get; }
    public float InitialAngle { get; }
    public HazardResponse Response { get; }

    public HazardDefinition(string id, Vector3 pivot, Vector3 axis, Vector3 centerOffset,
        Vector3 halfExtents, Quaternion initialOrientation, float angularSpeed, float initialAngle,
        HazardResponse response = HazardResponse.Fail)
    {
        Id = LevelValidation.Id(id, nameof(id));
        LevelValidation.Position(pivot, nameof(pivot));
        LevelValidation.Position(centerOffset, nameof(centerOffset));
        LevelValidation.Extents(halfExtents, nameof(halfExtents));
        LevelValidation.Finite(angularSpeed, nameof(angularSpeed));
        LevelValidation.Finite(initialAngle, nameof(initialAngle));
        if (!Enum.IsDefined(response)) throw new ArgumentOutOfRangeException(nameof(response));
        Pivot = pivot;
        Axis = LevelValidation.Direction(axis, nameof(axis));
        CenterOffset = centerOffset;
        HalfExtents = halfExtents;
        InitialOrientation = LevelValidation.Rotation(initialOrientation, nameof(initialOrientation));
        AngularSpeed = angularSpeed;
        InitialAngle = initialAngle;
        Response = response;
    }
}
