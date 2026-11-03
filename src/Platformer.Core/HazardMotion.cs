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

        // Сначала редуцируем время, чтобы умножение на скорость не переполнило double.
        double angle = hazard.InitialAngle;
        if (hazard.AngularSpeed != 0f)
        {
            double period = Math.Tau / Math.Abs((double)hazard.AngularSpeed);
            angle += Math.IEEERemainder(simulationSeconds, period) * hazard.AngularSpeed;
        }
        angle = Math.IEEERemainder(angle, Math.Tau);
        var worldRotation = Quaternion.CreateFromAxisAngle(hazard.Axis, (float)angle);
        Vector3 center = hazard.Pivot + Vector3.Transform(hazard.CenterOffset, worldRotation);
        Quaternion orientation = Quaternion.Normalize(Quaternion.Concatenate(hazard.InitialOrientation, worldRotation));
        return new HazardPose(hazard.Id, new OrientedBox(center, hazard.HalfExtents, orientation));
    }
}
