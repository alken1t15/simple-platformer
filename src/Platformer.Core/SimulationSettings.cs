namespace Platformer.Core;

/// <summary>Единый неизменяемый набор параметров Core v1, секунды и мировые единицы.</summary>
public sealed class SimulationSettings
{
    public static SimulationSettings Default { get; } = new();

    public float FixedStepSeconds { get; }
    public float BallRadius { get; }
    public float MoveSpeed { get; }
    public float MoveAcceleration { get; }
    public float BrakingAcceleration { get; }
    public float AirControlFactor { get; }
    public float Gravity { get; }
    public float JumpSpeed { get; }
    public float BoostMultiplier { get; }
    public float BoostDurationSeconds { get; }
    public float BoostCooldownSeconds { get; }
    public float MaxSlopeRadians { get; }
    public float SkinWidth { get; }
    public float PositionEpsilon { get; }
    public float PenetrationSlop { get; }
    public int MaxCollisionSubsteps { get; }
    public int MaxContactIterations { get; }
    public float MaxFallSpeed { get; }

    public SimulationSettings(
        float fixedStepSeconds = 1f / 120f, float ballRadius = 0.5f,
        float moveSpeed = 4.5f, float moveAcceleration = 20f, float brakingAcceleration = 24f,
        float airControlFactor = 0.25f, float gravity = 18f, float jumpSpeed = 6f,
        float boostMultiplier = 1.8f, float boostDurationSeconds = 1f, float boostCooldownSeconds = 3f,
        float maxSlopeRadians = 50f * MathF.PI / 180f,
        float skinWidth = 0.001f, float positionEpsilon = 1e-5f, float penetrationSlop = 1e-4f,
        int maxCollisionSubsteps = 32, int maxContactIterations = 8, float maxFallSpeed = 30f)
    {
        Range(fixedStepSeconds, 1f / 240f, 1f / 30f, nameof(fixedStepSeconds));
        Range(ballRadius, 0.1f, 2f, nameof(ballRadius));
        Positive(moveSpeed, 20f, nameof(moveSpeed));
        Positive(moveAcceleration, 100f, nameof(moveAcceleration));
        Positive(brakingAcceleration, 100f, nameof(brakingAcceleration));
        Range(airControlFactor, 0f, 1f, nameof(airControlFactor));
        Positive(gravity, 100f, nameof(gravity));
        Positive(jumpSpeed, 20f, nameof(jumpSpeed));
        Range(boostMultiplier, 1f, 3f, nameof(boostMultiplier));
        Positive(boostDurationSeconds, 5f, nameof(boostDurationSeconds));
        Range(boostCooldownSeconds, 0f, 10f, nameof(boostCooldownSeconds));
        Positive(maxSlopeRadians, 80f * MathF.PI / 180f, nameof(maxSlopeRadians));
        Range(skinWidth, 1e-4f, 0.01f, nameof(skinWidth));
        Range(positionEpsilon, 1e-6f, 1e-4f, nameof(positionEpsilon));
        Range(penetrationSlop, positionEpsilon, skinWidth, nameof(penetrationSlop));
        Range(maxFallSpeed, jumpSpeed, 100f, nameof(maxFallSpeed));
        if (skinWidth >= ballRadius / 10f)
            throw new ArgumentOutOfRangeException(nameof(skinWidth));
        if (positionEpsilon >= skinWidth)
            throw new ArgumentOutOfRangeException(nameof(positionEpsilon));
        if (maxCollisionSubsteps is < 1 or > 128)
            throw new ArgumentOutOfRangeException(nameof(maxCollisionSubsteps));
        if (maxContactIterations is < 1 or > 32)
            throw new ArgumentOutOfRangeException(nameof(maxContactIterations));

        double horizontal = (double)moveSpeed * boostMultiplier;
        double distance = Math.Sqrt(horizontal * horizontal + (double)maxFallSpeed * maxFallSpeed) * fixedStepSeconds;
        if (Math.Ceiling(distance / (ballRadius / 4d)) > maxCollisionSubsteps)
            throw new ArgumentOutOfRangeException(nameof(maxCollisionSubsteps),
                "Бюджет не покрывает скорость boost/падения при выбранных radius и fixedStepSeconds");

        FixedStepSeconds = fixedStepSeconds;
        BallRadius = ballRadius;
        MoveSpeed = moveSpeed;
        MoveAcceleration = moveAcceleration;
        BrakingAcceleration = brakingAcceleration;
        AirControlFactor = airControlFactor;
        Gravity = gravity;
        JumpSpeed = jumpSpeed;
        BoostMultiplier = boostMultiplier;
        BoostDurationSeconds = boostDurationSeconds;
        BoostCooldownSeconds = boostCooldownSeconds;
        MaxSlopeRadians = maxSlopeRadians;
        SkinWidth = skinWidth;
        PositionEpsilon = positionEpsilon;
        PenetrationSlop = penetrationSlop;
        MaxCollisionSubsteps = maxCollisionSubsteps;
        MaxContactIterations = maxContactIterations;
        MaxFallSpeed = maxFallSpeed;
    }

    private static void Range(float value, float minimum, float maximum, string parameter)
    {
        if (!float.IsFinite(value) || value < minimum || value > maximum)
            throw new ArgumentOutOfRangeException(parameter);
    }

    private static void Positive(float value, float maximum, string parameter)
    {
        if (!float.IsFinite(value) || value <= 0f || value > maximum)
            throw new ArgumentOutOfRangeException(parameter);
    }
}
