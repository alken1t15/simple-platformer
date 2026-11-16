using System.Numerics;

namespace Platformer.Core;

/// <summary>Кинематическая опора движется между двумя крайними положениями, начиная в центре.</summary>
public sealed class PlatformMotion
{
    public Vector3 Travel { get; }
    public float Period { get; }
    public PlatformMotion(Vector3 travel, float period)
    {
        LevelValidation.Finite(travel, nameof(travel));
        if (travel.Length() > 8 || !float.IsFinite(period) || period is < 8 or > 60)
            throw new ArgumentOutOfRangeException(nameof(period));
        Travel = travel; Period = period;
    }
    public Vector3 Offset(double seconds) => Travel * MathF.Sin((float)(Math.IEEERemainder(seconds, Period) * Math.Tau / Period));
}

public sealed record SpringPad
{
    public string Id { get; }
    public OrientedBox Bounds { get; }
    public float LaunchSpeed { get; }
    public SpringPad(string id, OrientedBox bounds, float launchSpeed = 11)
    {
        Id = LevelValidation.Id(id, nameof(id)); Bounds = bounds ?? throw new ArgumentNullException(nameof(bounds));
        if (!float.IsFinite(launchSpeed) || launchSpeed is < 8 or > 15) throw new ArgumentOutOfRangeException(nameof(launchSpeed));
        LaunchSpeed = launchSpeed;
    }
}

public sealed record WindZone
{
    public string Id { get; }
    public OrientedBox Bounds { get; }
    public Vector3 Acceleration { get; }
    public WindZone(string id, OrientedBox bounds, Vector3 acceleration)
    {
        Id = LevelValidation.Id(id, nameof(id)); Bounds = bounds ?? throw new ArgumentNullException(nameof(bounds));
        LevelValidation.Finite(acceleration, nameof(acceleration));
        if (acceleration.Length() > 12) throw new ArgumentOutOfRangeException(nameof(acceleration));
        Acceleration = acceleration;
    }
}
