using System.Numerics;
using Platformer.Core;

namespace Platformer.App;

public readonly record struct CameraPose(Vector3 Position, Vector3 Target, float YawRadians, float PitchRadians);

/// <summary>CPU-орбита; target — уже ball.Center + (0, 0.3, 0). Не меняет состояние шара.</summary>
public sealed class CameraRig
{
    public const float DefaultDistance = 6f;
    public const float DefaultPitch = 0.4f;
    public const float MinimumPitch = -0.35f;
    public const float MaximumPitch = 1.2f;
    public const float MouseSensitivity = 0.003f;
    public const float ObstructionRadius = 0.2f;
    public const float WallGap = 0.05f;
    public float YawRadians { get; private set; }
    public float PitchRadians { get; private set; } = DefaultPitch;
    public float Distance { get; private set; } = DefaultDistance;
    public bool TargetObstructed { get; private set; }
    public bool CastBudgetLimited { get; private set; }

    public void Reset()
    {
        YawRadians = 0f;
        PitchRadians = DefaultPitch;
        Distance = DefaultDistance;
        TargetObstructed = CastBudgetLimited = false;
    }

    public CameraPose Update(Vector2 relativeMouseDelta, Vector3 target, LevelDefinition level, float frameSeconds)
    {
        ArgumentNullException.ThrowIfNull(level);
        if (!float.IsFinite(frameSeconds) || frameSeconds < 0f)
            throw new ArgumentOutOfRangeException(nameof(frameSeconds));
        if (!float.IsFinite(relativeMouseDelta.X) || !float.IsFinite(relativeMouseDelta.Y))
            throw new ArgumentOutOfRangeException(nameof(relativeMouseDelta));
        if (!float.IsFinite(target.X) || !float.IsFinite(target.Y) || !float.IsFinite(target.Z)
            || MathF.Abs(target.X) > 10000f || MathF.Abs(target.Y) > 10000f || MathF.Abs(target.Z) > 10000f)
            throw new ArgumentOutOfRangeException(nameof(target));

        // Эквивалентный угол сохраняет точность sin/cos после длительного вращения.
        YawRadians = (float)Math.IEEERemainder((double)YawRadians - relativeMouseDelta.X * (double)MouseSensitivity, Math.Tau);
        PitchRadians = (float)Math.Clamp((double)PitchRadians - relativeMouseDelta.Y * (double)MouseSensitivity,
            MinimumPitch, MaximumPitch);
        Vector3 direction = new(MathF.Sin(YawRadians) * MathF.Cos(PitchRadians),
            MathF.Sin(PitchRadians), MathF.Cos(YawRadians) * MathF.Cos(PitchRadians));
        float allowed = DefaultDistance;
        TargetObstructed = CastBudgetLimited = false;
        foreach (var surface in level.Surfaces)
        {
            float contact = Cast(surface.Bounds, target, direction);
            allowed = MathF.Min(allowed, contact < DefaultDistance ? MathF.Max(0f, contact - WallGap) : DefaultDistance);
        }
        Distance = allowed < Distance ? allowed
            : MathF.Min(allowed, Distance + (DefaultDistance - Distance) * (1f - MathF.Exp(-8f * frameSeconds)));
        Vector3 position = target + direction * Distance;
        // При нулевом расстоянии сохраняем ориентацию; LookAt никогда не получает совпавшие точки.
        Vector3 lookTarget = Distance < 0.001f ? position - direction : target;
        return new CameraPose(position, lookTarget, YawRadians, PitchRadians);
    }

    private float Cast(OrientedBox box, Vector3 target, Vector3 direction)
    {
        Vector3 origin = box.ToLocal(target);
        Vector3 ray = Vector3.Transform(direction, Quaternion.Conjugate(box.Orientation));
        float distance = 0f;
        // Conservative advancement по точному расстоянию до OBB (включая круглые рёбра).
        // Опорная плоскость выпуклой функции расстояния даёт нижнюю границу пути до контакта.
        for (int iteration = 0; iteration < 128; iteration++)
        {
            Vector3 point = origin + ray * distance;
            Vector3 offset = point - Vector3.Clamp(point, -box.HalfExtents, box.HalfExtents);
            float length = offset.Length();
            float gap = length - ObstructionRadius;
            if (gap <= 0.00001f)
            {
                if (distance == 0f) TargetObstructed = true;
                return distance;
            }
            float approach = -Vector3.Dot(ray, offset / length);
            // Расстояние до выпуклого OBB вдоль луча уже не уменьшается: контакта впереди нет.
            if (approach <= 0f) return DefaultDistance;
            float advance = gap / approach;
            if (distance + advance >= DefaultDistance) return DefaultDistance;
            distance += advance * 0.99f;
        }
        // Почти касательная траектория может исчерпать бюджет. Остановиться безопаснее, чем пройти стену.
        CastBudgetLimited = true;
        return distance;
    }
}
