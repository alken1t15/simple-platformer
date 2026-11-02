namespace Platformer.Core;

public sealed class SurfaceDefinition
{
    public string Id { get; }
    public OrientedBox Bounds { get; }

    public SurfaceDefinition(string id, OrientedBox bounds)
    {
        Id = LevelValidation.Id(id, nameof(id));
        ArgumentNullException.ThrowIfNull(bounds);
        Bounds = bounds;
    }
}
