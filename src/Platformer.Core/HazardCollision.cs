using System.Numerics;

namespace Platformer.Core;

/// <summary>Консервативная проверка непрерывного движения шара и вращающейся балки.</summary>
public static class HazardCollision
{
    public static bool IntersectsPath(HazardDefinition hazard, IReadOnlyList<MotionSegment> path,
        double tickStartSeconds, SimulationSettings settings)
    {
        ArgumentNullException.ThrowIfNull(hazard);
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(settings);
        if (!double.IsFinite(tickStartSeconds) || tickStartSeconds < 0d)
            throw new ArgumentOutOfRangeException(nameof(tickStartSeconds));
        if (path.Count == 0) throw new ArgumentException("Путь пуст", nameof(path));

        float previousFraction = 0f;
        Vector3 previousEnd = path[0].Start;
        foreach (var segment in path)
        {
            LevelValidation.Position(segment.Start, nameof(path));
            LevelValidation.Position(segment.End, nameof(path));
            if (!float.IsFinite(segment.StartFraction) || !float.IsFinite(segment.EndFraction)
                || segment.StartFraction < 0f || segment.EndFraction > 1f
                || segment.EndFraction <= segment.StartFraction
                || MathF.Abs(segment.StartFraction - previousFraction) > settings.PositionEpsilon
                || Vector3.Distance(segment.Start, previousEnd) > settings.PositionEpsilon)
                throw new ArgumentException("Путь должен быть непрерывным и покрывать [0,1]", nameof(path));
            previousFraction = segment.EndFraction;
            previousEnd = segment.End;
        }
        if (MathF.Abs(previousFraction - 1f) > settings.PositionEpsilon)
            throw new ArgumentException("Путь должен покрывать [0,1]", nameof(path));

        // Максимальная скорость любой точки OBB ограничена угловой скоростью и
        // расстоянием от оси до самой удалённой вершины, включая centerOffset.
        double angularTravel = HazardMotion.MaximumPointSpeed(hazard) * settings.FixedStepSeconds;
        double requiredSteps = 0d;
        foreach (var segment in path)
        {
            double travel = Vector3.Distance(segment.Start, segment.End)
                + angularTravel * (segment.EndFraction - segment.StartFraction);
            requiredSteps += Math.Max(1d, Math.Ceiling(travel / (settings.BallRadius / 4d)));
        }
        if (requiredSteps > settings.MaxCollisionSubsteps)
            throw new InvalidOperationException($"Исчерпан бюджет проверки опасности {hazard.Id}");
        int used = 0;
        foreach (var segment in path)
        {
            var startPose = HazardMotion.Evaluate(hazard, tickStartSeconds + segment.StartFraction * settings.FixedStepSeconds);
            var endPose = HazardMotion.Evaluate(hazard, tickStartSeconds + segment.EndFraction * settings.FixedStepSeconds);
            if (SphereGeometry.Contact(startPose.Bounds, segment.Start, settings.BallRadius).Separation <= 0f
                || SphereGeometry.Contact(endPose.Bounds, segment.End, settings.BallRadius).Separation <= 0f)
                return true;
            if (Check(segment, 0d, 1d)) return true;
        }
        return false;

        bool Check(MotionSegment segment, double from, double to)
        {
            if (++used > settings.MaxCollisionSubsteps)
                throw new InvalidOperationException($"Исчерпан бюджет проверки опасности {hazard.Id}");
            double middle = (from + to) * 0.5d;
            double global = segment.StartFraction + (segment.EndFraction - segment.StartFraction) * middle;
            var point = Vector3.Lerp(segment.Start, segment.End, (float)middle);
            var pose = HazardMotion.Evaluate(hazard, tickStartSeconds + global * settings.FixedStepSeconds);
            double separation = SphereGeometry.Contact(pose.Bounds, point, settings.BallRadius).Separation;
            if (separation <= 0d) return true;
            double ballTravel = Vector3.Distance(segment.Start, segment.End) * (to - from) * 0.5d;
            double beamTravel = angularTravel * (segment.EndFraction - segment.StartFraction) * (to - from) * 0.5d;
            // Если даже верхняя граница относительного перемещения меньше зазора,
            // во всём интервале контакта быть не могло.
            if (separation > ballTravel + beamTravel + settings.PositionEpsilon) return false;
            if (to - from < 1e-12d)
                throw new InvalidOperationException($"Не удалось разрешить касание опасности {hazard.Id}");
            return Check(segment, from, middle) || Check(segment, middle, to);
        }
    }
}
