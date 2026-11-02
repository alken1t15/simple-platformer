using System.Numerics;

namespace Platformer.Core;

public enum SessionPhase { Running, Failed, LevelComplete }

public readonly record struct HazardPose(string Id, OrientedBox Bounds);

/// <summary>Общий неизменяемый контракт для сеанса, Presenter и HUD; не выполняет Step.</summary>
public sealed class SessionSnapshot
{
    public Guid SessionId { get; }
    public string LevelId { get; }
    public long Tick { get; }
    public double SimulationSeconds { get; }
    public SessionPhase Phase { get; }
    public BallState Ball { get; }
    public IReadOnlyList<string> CollectedCoinIds { get; }
    public IReadOnlyList<HazardPose> Hazards { get; }
    public int CoinCount => CollectedCoinIds.Count;

    public SessionSnapshot(Guid sessionId, string levelId, long tick, double simulationSeconds,
        SessionPhase phase, BallState ball, IReadOnlyList<string> collectedCoinIds,
        IReadOnlyList<HazardPose> hazards)
    {
        if (sessionId == Guid.Empty) throw new ArgumentException("Требуется ID сеанса", nameof(sessionId));
        LevelId = LevelValidation.Id(levelId, nameof(levelId));
        ArgumentOutOfRangeException.ThrowIfNegative(tick);
        if (!double.IsFinite(simulationSeconds) || simulationSeconds < 0d)
            throw new ArgumentOutOfRangeException(nameof(simulationSeconds));
        if (!Enum.IsDefined(phase)) throw new ArgumentOutOfRangeException(nameof(phase));
        LevelValidation.Position(ball.Center, nameof(ball));
        LevelValidation.Finite(ball.Velocity, nameof(ball));
        LevelValidation.Finite(ball.GroundNormal, nameof(ball));
        if (!float.IsFinite(ball.Radius) || ball.Radius is < 0.1f or > 2f
            || !float.IsFinite(ball.BoostRemainingSeconds) || ball.BoostRemainingSeconds < 0f
            || !float.IsFinite(ball.BoostCooldownSeconds) || ball.BoostCooldownSeconds < 0f
            || (ball.Grounded && MathF.Abs(ball.GroundNormal.LengthSquared() - 1f) > 1e-3f))
            throw new ArgumentOutOfRangeException(nameof(ball));
        ArgumentNullException.ThrowIfNull(collectedCoinIds);
        ArgumentNullException.ThrowIfNull(hazards);
        var coins = collectedCoinIds.ToArray();
        var poses = hazards.ToArray();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (string id in coins)
            if (!ids.Add(LevelValidation.Id(id, nameof(collectedCoinIds))))
                throw new ArgumentException($"Повторная монета {id}", nameof(collectedCoinIds));
        ids.Clear();
        foreach (var pose in poses)
        {
            if (pose.Bounds is null || !ids.Add(LevelValidation.Id(pose.Id, nameof(hazards))))
                throw new ArgumentException($"Некорректная поза опасности {pose.Id}", nameof(hazards));
        }
        Array.Sort(coins, StringComparer.Ordinal);
        Array.Sort(poses, (left, right) => StringComparer.Ordinal.Compare(left.Id, right.Id));
        SessionId = sessionId;
        Tick = tick;
        SimulationSeconds = simulationSeconds;
        Phase = phase;
        Ball = ball;
        CollectedCoinIds = Array.AsReadOnly(coins);
        Hazards = Array.AsReadOnly(poses);
    }
}
