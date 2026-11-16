namespace Platformer.Core;

public sealed class SurfaceDefinition
{
    public string Id { get; }
    public OrientedBox Bounds { get; }
    /// <summary>0 — постоянная опора; иначе время от первого касания до разрушения.</summary>
    public float CollapseDelaySeconds { get; }
    /// <summary>Сплошной борт: те же OBB для физики и отдельное оформление в App.</summary>
    public bool IsBarrier { get; }
    public float Traction { get; }
    public PlatformMotion? Motion { get; }
    public OrientedBox BoundsAt(double seconds) => Motion is null ? Bounds
        : new(Bounds.Center + Motion.Offset(seconds), Bounds.HalfExtents, Bounds.Orientation);

    public SurfaceDefinition(string id, OrientedBox bounds, float collapseDelaySeconds = 0f, bool isBarrier = false, float traction = 1f, PlatformMotion? motion = null)
    {
        Id = LevelValidation.Id(id, nameof(id));
        ArgumentNullException.ThrowIfNull(bounds);
        Bounds = bounds;
        if (!float.IsFinite(collapseDelaySeconds) || collapseDelaySeconds < 0f || collapseDelaySeconds > 30f
            || (collapseDelaySeconds > 0f && collapseDelaySeconds < 0.2f))
            throw new ArgumentOutOfRangeException(nameof(collapseDelaySeconds));
        CollapseDelaySeconds = collapseDelaySeconds;
        IsBarrier = isBarrier;
        if (!float.IsFinite(traction) || traction is < 0.05f or > 1f) throw new ArgumentOutOfRangeException(nameof(traction));
        Traction = traction;
        if (motion is not null && (isBarrier || collapseDelaySeconds > 0)) throw new ArgumentException("Подвижная опора не может быть разрушаемой или бортом", nameof(motion));
        Motion = motion;
    }
}
