using System.Numerics;

namespace Platformer.Core;

public sealed class CoinDefinition
{
    public string Id { get; }
    public Vector3 Center { get; }
    public float Radius { get; }

    public CoinDefinition(string id, Vector3 center, float radius)
    {
        Id = LevelValidation.Id(id, nameof(id));
        LevelValidation.Position(center, nameof(center));
        LevelValidation.Size(radius, nameof(radius));
        Center = center;
        Radius = radius;
    }
}
