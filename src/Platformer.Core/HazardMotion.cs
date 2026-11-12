using System.Numerics;

namespace Platformer.Core;

/// <summary>Поза опасности определяется исключительно временем симуляции.</summary>
public static class HazardMotion
{
    public static HazardPose Evaluate(HazardDefinition hazard, double simulationSeconds)
    {
        ArgumentNullException.ThrowIfNull(hazard);
        if (!double.IsFinite(simulationSeconds) || simulationSeconds < 0d)
            throw new ArgumentOutOfRangeException(nameof(simulationSeconds));

        double phase = Phase(hazard, simulationSeconds);
        if (hazard.MotionKind == HazardMotionKind.Shuttle)
            return new(hazard.Id, new(hazard.Pivot + hazard.CenterOffset
                + hazard.Axis * (hazard.Amplitude * (float)Math.Sin(phase)), hazard.HalfExtents, hazard.InitialOrientation));
        double angle = hazard.MotionKind == HazardMotionKind.Pendulum ? hazard.Amplitude * Math.Sin(phase) : phase;
        var worldRotation = Quaternion.CreateFromAxisAngle(hazard.Axis, (float)angle);
        Vector3 center = hazard.Pivot + Vector3.Transform(hazard.CenterOffset, worldRotation);
        Quaternion orientation = Quaternion.Normalize(Quaternion.Concatenate(hazard.InitialOrientation, worldRotation));
        return new HazardPose(hazard.Id, new OrientedBox(center, hazard.HalfExtents, orientation));
    }

    /// <summary>Верхняя граница скорости любой вершины OBB, в том числе при смене направления.</summary>
    public static double MaximumPointSpeed(HazardDefinition hazard)
    {
        ArgumentNullException.ThrowIfNull(hazard);
        double speed = Math.Abs((double)hazard.AngularSpeed);
        return hazard.MotionKind == HazardMotionKind.Shuttle ? speed * hazard.Amplitude
            : speed * (hazard.MotionKind == HazardMotionKind.Pendulum ? hazard.Amplitude : 1)
                * ((double)hazard.CenterOffset.Length() + hazard.HalfExtents.Length());
    }

    /// <summary>Мгновенная скорость точки опасности для выбора направления отскока.</summary>
    public static Vector3 VelocityAt(HazardDefinition hazard, double simulationSeconds, Vector3 point)
    {
        // Evaluate валидирует время и ссылку одинаково для всех режимов.
        _ = Evaluate(hazard, simulationSeconds);
        float rate = hazard.AngularSpeed;
        if (hazard.MotionKind != HazardMotionKind.Rotation)
            rate *= hazard.Amplitude * (float)Math.Cos(Phase(hazard, simulationSeconds));
        return hazard.MotionKind == HazardMotionKind.Shuttle ? hazard.Axis * rate
            : Vector3.Cross(hazard.Axis * rate, point - hazard.Pivot);
    }

    private static double Phase(HazardDefinition hazard, double simulationSeconds)
    {
        // Сначала редуцируем время, чтобы умножение на скорость не переполнило double.
        double angle = hazard.InitialAngle;
        if (hazard.AngularSpeed != 0f)
        {
            double period = Math.Tau / Math.Abs((double)hazard.AngularSpeed);
            angle += Math.IEEERemainder(simulationSeconds, period) * hazard.AngularSpeed;
        }
        return Math.IEEERemainder(angle, Math.Tau);
    }
}
