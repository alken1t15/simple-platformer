using System.Numerics;

namespace Platformer.Core;

public readonly record struct MotionSegment(Vector3 Start, Vector3 End, float StartFraction, float EndFraction);

/// <summary>Отделённый результат tick; Path сохраняется после следующих вызовов Step.</summary>
public sealed class BallStepResult
{
    public BallState Ball { get; }
    public IReadOnlyList<MotionSegment> Path { get; }

    internal BallStepResult(BallState ball, List<MotionSegment> path)
    {
        Ball = ball;
        Path = Array.AsReadOnly(path.ToArray());
    }
}
