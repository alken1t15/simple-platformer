using System.Numerics;

namespace Platformer.Core;

/// <summary>Чистый шаг одной сферы по статическим поверхностям. Все рабочие данные локальны вызову.</summary>
public sealed class BallSimulator
{
    private readonly LevelDefinition level;
    private readonly SimulationSettings settings;
    private readonly SurfaceDefinition[] surfaces;
    private readonly float minimumGroundY;

    public BallSimulator(LevelDefinition level, SimulationSettings settings)
    {
        ArgumentNullException.ThrowIfNull(level);
        ArgumentNullException.ThrowIfNull(settings);
        this.level = level;
        this.settings = settings;
        surfaces = level.Surfaces.OrderBy(surface => surface.Id, StringComparer.Ordinal).ToArray();
        minimumGroundY = MathF.Cos(settings.MaxSlopeRadians);
    }

    public BallState CreateInitialState()
    {
        Vector3 center = level.Spawn;
        foreach (var surface in surfaces)
        {
            var contact = SphereGeometry.Contact(surface.Bounds, center, settings.BallRadius);
            if (contact.Separation < -settings.SkinWidth - settings.PositionEpsilon)
                throw new ArgumentException($"Глубокое проникновение spawn в поверхность '{surface.Id}'", nameof(level));
        }
        string? lastSurface = null;
        for (int iteration = 0; ; iteration++)
        {
            SurfaceDefinition? overlapping = null;
            SphereContact deepest = default;
            foreach (var surface in surfaces)
            {
                var contact = SphereGeometry.Contact(surface.Bounds, center, settings.BallRadius);
                if (contact.Separation < -settings.PositionEpsilon
                    && (overlapping is null || contact.Separation < deepest.Separation - settings.PositionEpsilon))
                {
                    overlapping = surface;
                    deepest = contact;
                }
            }
            if (overlapping is null) break;
            if (iteration >= settings.MaxContactIterations)
                throw BudgetError(lastSurface ?? overlapping.Id, nameof(settings.MaxContactIterations));
            lastSurface = overlapping.Id;
            center += deepest.Normal * (-deepest.Separation + ContactPadding(center, overlapping.Bounds));
        }
        LevelValidation.Position(center, nameof(level.Spawn));
        Vector3 normal = ProbeGround(center, Vector3.Zero);
        return new BallState(center, Vector3.Zero, settings.BallRadius, normal != Vector3.Zero, normal, 0f, 0f);
    }

    public BallStepResult Step(BallState before, GameCommand command)
    {
        Vector2 move = command.ValidatedMove();
        Validate(before);
        // Геометрическая проекция spawn выполняется CreateInitialState, а не телепортом
        // внутри временного Path. Произвольное проникающее состояние отклоняется до tick.
        CheckPenetration(before.Center, true);
        float dt = settings.FixedStepSeconds;
        float boost = before.BoostRemainingSeconds;
        float cooldown = before.BoostCooldownSeconds;
        if (command.BoostPressed && boost == 0f && cooldown == 0f)
            boost = settings.BoostDurationSeconds;
        float speed = settings.MoveSpeed * (boost > 0f ? settings.BoostMultiplier : 1f);

        Vector3 velocity = before.Velocity;
        Vector3 ground = ProbeGround(before.Center, velocity);
        bool grounded = ground != Vector3.Zero;
        if (grounded && command.JumpPressed)
        {
            velocity.Y = settings.JumpSpeed;
            grounded = false;
        }
        Vector3 desired = new(move.X * speed, 0f, move.Y * speed);
        float acceleration = move == Vector2.Zero ? settings.BrakingAcceleration : settings.MoveAcceleration;
        if (grounded)
        {
            velocity -= ground * Vector3.Dot(velocity, ground);
            desired -= ground * Vector3.Dot(desired, ground);
            velocity = Approach(velocity, desired, acceleration * dt);
        }
        else
        {
            Vector3 horizontal = Approach(new Vector3(velocity.X, 0f, velocity.Z), desired,
                acceleration * settings.AirControlFactor * dt);
            velocity = new Vector3(horizontal.X, MathF.Max(-settings.MaxFallSpeed, velocity.Y - settings.Gravity * dt), horizontal.Z);
        }

        double distance = Math.Sqrt((double)velocity.X * velocity.X + (double)velocity.Y * velocity.Y
            + (double)velocity.Z * velocity.Z) * dt;
        double required = Math.Max(1d, Math.Ceiling(distance / (settings.BallRadius / 4d)));
        if (required > settings.MaxCollisionSubsteps)
            throw BudgetError(level.Id, nameof(settings.MaxCollisionSubsteps));
        int substeps = (int)required;
        Vector3 center = before.Center;
        var path = new List<MotionSegment>();
        for (int substep = 0; substep < substeps; substep++)
            Move(ref center, ref velocity, (double)substep / substeps, (double)(substep + 1) / substeps, path);

        LevelValidation.Position(center, nameof(before.Center));
        CheckPenetration(center, false);
        ground = ProbeGround(center, velocity);
        AdvanceTimers(ref boost, ref cooldown);
        var ball = new BallState(center, velocity, settings.BallRadius, ground != Vector3.Zero, ground, boost, cooldown);
        return new BallStepResult(ball, path);
    }

    private void Move(ref Vector3 center, ref Vector3 velocity, double start, double end, List<MotionSegment> path)
    {
        double time = start;
        int contacts = 0;
        Span<Vector3> planes = stackalloc Vector3[settings.MaxContactIterations];
        Span<int> planeSurfaces = stackalloc int[settings.MaxContactIterations];
        int planeCount = 0;
        while (time < end)
        {
            Vector3 displacement = velocity * (float)((end - time) * settings.FixedStepSeconds);
            double firstFraction = 1d;
            Vector3 normal = Vector3.Zero;
            SurfaceDefinition? blocking = null;
            int blockingIndex = -1;
            for (int surfaceIndex = 0; surfaceIndex < surfaces.Length; surfaceIndex++)
            {
                var surface = surfaces[surfaceIndex];
                if (SphereGeometry.Sweep(surface.Bounds, center, displacement, settings.BallRadius + ContactPadding(center, surface.Bounds),
                    out double fraction, out Vector3 candidateNormal)
                    && Dot(velocity, candidateNormal) < 0d
                    && (blocking is null || fraction < firstFraction - 1e-9d))
                {
                    firstFraction = fraction;
                    normal = candidateNormal;
                    blocking = surface;
                    blockingIndex = surfaceIndex;
                }
            }
            if (blocking is null)
            {
                Vector3 destination = center + displacement;
                Append(path, center, destination, time, end);
                center = destination;
                return;
            }
            if (contacts++ >= settings.MaxContactIterations)
                throw BudgetError(blocking.Id, nameof(settings.MaxContactIterations));
            double hitTime = time + (end - time) * firstFraction;
            Vector3 hit = center + displacement * (float)firstFraction;
            Append(path, center, hit, time, hitTime);
            // Несколько ударов в одной точке должны удовлетворять всем уже найденным
            // нормалям: скольжение только по последней может снова вдавить шар в соседний край.
            if (Vector3.DistanceSquared(center, hit) > settings.PositionEpsilon * settings.PositionEpsilon)
                planeCount = 0;
            center = hit;
            int planeIndex = planeSurfaces[..planeCount].IndexOf(blockingIndex);
            if (planeIndex < 0) { planeIndex = planeCount++; planeSurfaces[planeIndex] = blockingIndex; }
            planes[planeIndex] = normal;
            velocity = ConstrainVelocity(velocity, planes[..planeCount]);
            time = hitTime;
        }
    }

    private static Vector3 ConstrainVelocity(Vector3 velocity, ReadOnlySpan<Vector3> planes)
    {
        if (planes.Length == 1) return Slide(velocity, planes[0]);
        // В 3D ближайшая допустимая скорость лежит на одной плоскости, линии
        // пересечения двух плоскостей либо в начале координат (полный упор).
        Vector3 best = Vector3.Zero;
        double bestChange = Dot(velocity, velocity);
        for (int first = 0; first < planes.Length; first++)
        {
            ChooseVelocity(velocity, Slide(velocity, planes[first]), planes, ref best, ref bestChange);
            for (int second = 0; second < first; second++)
            {
                Vector3 crease = Vector3.Cross(planes[first], planes[second]);
                double lengthSquared = Dot(crease, crease);
                if (lengthSquared <= 1e-20d) continue;
                Vector3 candidate = crease * (float)(Dot(velocity, crease) / lengthSquared);
                ChooseVelocity(velocity, candidate, planes, ref best, ref bestChange);
            }
        }
        return best;
    }

    private static void ChooseVelocity(Vector3 original, Vector3 candidate, ReadOnlySpan<Vector3> planes,
        ref Vector3 best, ref double bestChange)
    {
        // Cross/float могут оставить несколько ULP входящей скорости на стыке OBB.
        // Исправляем только округление, не принимая геометрически недопустимый вариант.
        float magnitude = MathF.Max(1f, MathF.Max(MathF.Abs(candidate.X), MathF.Max(MathF.Abs(candidate.Y), MathF.Abs(candidate.Z))));
        double rounding = 8d * (MathF.BitIncrement(magnitude) - magnitude);
        foreach (Vector3 plane in planes)
            if (Dot(candidate, plane) < -rounding) return;
        Vector3 outward = Vector3.Zero;
        foreach (Vector3 plane in planes) outward += plane;
        for (int repair = 0; repair < 8; repair++)
        {
            bool valid = true;
            double correction = 0d;
            foreach (Vector3 plane in planes)
            {
                double incoming = Dot(candidate, plane);
                if (incoming >= 0d) continue;
                double away = Dot(outward, plane);
                if (away <= 0d) return;
                correction = Math.Max(correction, -incoming / away);
                valid = false;
            }
            if (valid)
            {
                double change = Dot(candidate - original, candidate - original);
                if (change < bestChange) { best = candidate; bestChange = change; }
                return;
            }
            candidate += outward * MathF.BitIncrement((float)correction);
        }
        // Нулевая скорость всегда допустима; непредставимую касательную не направляем в стену.
    }

    private void Append(List<MotionSegment> path, Vector3 start, Vector3 end, double startTime, double endTime)
    {
        float first = (float)startTime, last = (float)endTime;
        if (last > first)
            path.Add(new MotionSegment(start, end, first, last));
        else if (Vector3.Distance(start, end) > settings.PositionEpsilon)
            throw BudgetError(level.Id, "точность временного Path");
    }

    private Vector3 ProbeGround(Vector3 center, Vector3 velocity)
    {
        Vector3 normal = Vector3.Zero;
        float nearest = float.PositiveInfinity;
        Span<Vector3> supports = stackalloc Vector3[Math.Min(surfaces.Length, settings.MaxContactIterations)];
        int supportCount = 0;
        foreach (var surface in surfaces)
        {
            var contact = SphereGeometry.Contact(surface.Bounds, center, settings.BallRadius);
            if (contact.Separation >= -settings.PenetrationSlop
                && contact.Separation <= ContactPadding(center, surface.Bounds) + settings.PositionEpsilon
                && Vector3.Dot(velocity, contact.Normal) <= settings.PositionEpsilon)
            {
                if (contact.Normal.Y >= minimumGroundY && contact.Separation < nearest - settings.PositionEpsilon)
                {
                    normal = contact.Normal;
                    nearest = contact.Separation;
                }
                if (contact.Normal.Y > 0f && supportCount < supports.Length)
                    supports[supportCount++] = contact.Normal;
            }
        }
        // Два края могут вместе удерживать шар от падения, хотя каждый контакт
        // отдельно слишком крут. Такая опора должна разрешать прыжок, а не ловушку в щели.
        if (normal == Vector3.Zero && supportCount > 1
            && ConstrainVelocity(-Vector3.UnitY, supports[..supportCount]).LengthSquared() <= 1e-12f)
            return Vector3.UnitY;
        return normal;
    }

    private float ContactPadding(Vector3 center, OrientedBox bounds)
    {
        // Вдали от начала координат шаг float может превышать SkinWidth. Запас
        // на несколько ULP защищает local/world вычисления и округление точки
        // контакта, сохраняя физический Radius и строгую проверку проникновения.
        Vector3 scale = Vector3.Max(Vector3.Abs(center), Vector3.Max(Vector3.Abs(bounds.Center), bounds.HalfExtents));
        float magnitude = MathF.Max(scale.X, MathF.Max(scale.Y, scale.Z));
        float ulp = MathF.BitIncrement(magnitude) - magnitude;
        return MathF.Max(settings.SkinWidth, 4f * ulp);
    }

    private void CheckPenetration(Vector3 center, bool argument)
    {
        foreach (var surface in surfaces)
        {
            if (SphereGeometry.Contact(surface.Bounds, center, settings.BallRadius).Separation >= -settings.PenetrationSlop)
                continue;
            string message = $"Проникновение шара в поверхность '{surface.Id}'; начальное состояние создаётся CreateInitialState";
            if (argument) throw new ArgumentException(message, "before");
            throw new InvalidOperationException(message);
        }
    }

    private void Validate(BallState state)
    {
        LevelValidation.Position(state.Center, nameof(state.Center));
        LevelValidation.Finite(state.Velocity, nameof(state.Velocity));
        LevelValidation.Finite(state.Radius, nameof(state.Radius));
        if (MathF.Abs(state.Radius - settings.BallRadius) > settings.PositionEpsilon)
            throw new ArgumentOutOfRangeException(nameof(state.Radius));
        LevelValidation.Finite(state.GroundNormal, nameof(state.GroundNormal));
        if (state.Grounded && MathF.Abs(state.GroundNormal.LengthSquared() - 1f) > 1e-4f)
            throw new ArgumentOutOfRangeException(nameof(state.GroundNormal));
        if (!float.IsFinite(state.BoostRemainingSeconds) || state.BoostRemainingSeconds < 0f
            || state.BoostRemainingSeconds > settings.BoostDurationSeconds)
            throw new ArgumentOutOfRangeException(nameof(state.BoostRemainingSeconds));
        if (!float.IsFinite(state.BoostCooldownSeconds) || state.BoostCooldownSeconds < 0f
            || state.BoostCooldownSeconds > settings.BoostCooldownSeconds)
            throw new ArgumentOutOfRangeException(nameof(state.BoostCooldownSeconds));
        if (state.BoostRemainingSeconds > 0f && state.BoostCooldownSeconds > 0f)
            throw new ArgumentException("Boost и cooldown не могут идти одновременно", nameof(state));
    }

    private void AdvanceTimers(ref float boost, ref float cooldown)
    {
        if (boost > 0f)
        {
            float next = SubtractTick(boost);
            if (next > 0f) boost = next;
            else
            {
                float excess = MathF.Max(0f, settings.FixedStepSeconds - boost);
                boost = 0f;
                cooldown = MathF.Max(0f, settings.BoostCooldownSeconds - excess);
            }
        }
        else cooldown = SubtractTick(cooldown);
    }

    private float SubtractTick(float value)
    {
        // На сетке целых ticks восстанавливаем quotient перед вычитанием:
        // float-округления не добавляют лишний tick к длительности 1/3 секунд.
        double ticks = value / (double)settings.FixedStepSeconds;
        double nearest = Math.Round(ticks);
        if (Math.Abs(ticks - nearest) <= 4d * 1.1920928955078125e-7d * Math.Max(1d, ticks))
            ticks = nearest;
        return (float)(Math.Max(0d, ticks - 1d) * settings.FixedStepSeconds);
    }

    private static Vector3 Approach(Vector3 current, Vector3 target, float maximumDelta)
    {
        Vector3 delta = target - current;
        double length = Math.Sqrt((double)delta.X * delta.X + (double)delta.Y * delta.Y + (double)delta.Z * delta.Z);
        return length <= maximumDelta ? target : current + delta * (float)(maximumDelta / length);
    }

    private static double Dot(Vector3 a, Vector3 b) => (double)a.X * b.X + (double)a.Y * b.Y + (double)a.Z * b.Z;

    private static Vector3 Slide(Vector3 velocity, Vector3 normal)
    {
        double incoming = Dot(velocity, normal);
        if (incoming >= 0d) return velocity;
        velocity -= normal * (float)(incoming / Dot(normal, normal));
        // Округление float может оставить отрицательный dot и бесконечный контакт
        // с fraction=0. Восстанавливаем касательную компоненту по самой устойчивой
        // оси и выбираем соседний float наружу, если точная плоскость непредставима.
        Vector3 absolute = Vector3.Abs(normal);
        int axis = absolute.Y > absolute.X ? 1 : 0;
        if (absolute.Z > absolute[axis]) axis = 2;
        double other = 0d;
        for (int i = 0; i < 3; i++)
            if (i != axis) other += (double)velocity[i] * normal[i];
        velocity[axis] = (float)(-other / normal[axis]);
        if (Dot(velocity, normal) < 0d)
            velocity[axis] = normal[axis] > 0f ? MathF.BitIncrement(velocity[axis]) : MathF.BitDecrement(velocity[axis]);
        return velocity;
    }

    private static InvalidOperationException BudgetError(string id, string budget) =>
        new($"Не удалось завершить tick: '{id}', {budget}; частичный результат отклонён");
}
