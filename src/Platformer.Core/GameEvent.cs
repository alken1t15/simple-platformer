namespace Platformer.Core;

public enum GameEventKind { Jumped, BoostStarted, CoinCollected, Failed, LevelCompleted }

/// <summary>Ключ события — SessionId/Sequence; Tick относится к уже завершённому шагу.</summary>
public readonly record struct GameEvent(Guid SessionId, long Sequence, long Tick, GameEventKind Kind, string? EntityId);
