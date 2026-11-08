namespace Platformer.Core;

public sealed class SurfaceDefinition
{
    public string Id { get; }
    public OrientedBox Bounds { get; }
    /// <summary>0 — постоянная опора; иначе время от первого касания до разрушения.</summary>
    public float CollapseDelaySeconds { get; }

    public SurfaceDefinition(string id, OrientedBox bounds, float collapseDelaySeconds = 0f)
    {
        Id = LevelValidation.Id(id, nameof(id));
        ArgumentNullException.ThrowIfNull(bounds);
        Bounds = bounds;
        if (!float.IsFinite(collapseDelaySeconds) || collapseDelaySeconds < 0f || collapseDelaySeconds > 30f
            || (collapseDelaySeconds > 0f && collapseDelaySeconds < 0.2f))
            throw new ArgumentOutOfRangeException(nameof(collapseDelaySeconds));
        CollapseDelaySeconds = collapseDelaySeconds;
    }
}
