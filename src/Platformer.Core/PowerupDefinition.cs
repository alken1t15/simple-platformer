using System.Numerics;
namespace Platformer.Core;
public enum PowerupKind { Jump, Speed }
public sealed class PowerupDefinition
{
    public string Id { get; }
    public Vector3 Center { get; }
    public PowerupKind Kind { get; }
    public float DurationSeconds { get; }
    public PowerupDefinition(string id, Vector3 center, PowerupKind kind, float durationSeconds = 12)
    {
        Id = LevelValidation.Id(id, nameof(id)); LevelValidation.Position(center, nameof(center));
        if (!Enum.IsDefined(kind) || !float.IsFinite(durationSeconds) || durationSeconds is < 1 or > 30)
            throw new ArgumentOutOfRangeException(nameof(durationSeconds));
        Center=center; Kind=kind; DurationSeconds=durationSeconds;
    }
}
