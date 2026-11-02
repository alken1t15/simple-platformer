using System.Numerics;

namespace Platformer.Core;

/// <summary>Горизонтальное направление в мировых X/Z и фронты действий одного tick.</summary>
public readonly record struct GameCommand(Vector2 MoveWorldXZ, bool JumpPressed, bool BoostPressed)
{
    internal Vector2 ValidatedMove()
    {
        if (!float.IsFinite(MoveWorldXZ.X) || !float.IsFinite(MoveWorldXZ.Y))
            throw new ArgumentOutOfRangeException(nameof(MoveWorldXZ));
        double length = Math.Sqrt((double)MoveWorldXZ.X * MoveWorldXZ.X
            + (double)MoveWorldXZ.Y * MoveWorldXZ.Y);
        if (length > 1d + 1e-6d)
            throw new ArgumentOutOfRangeException(nameof(MoveWorldXZ), "Длина команды должна быть ≤1");
        return length > 1d ? MoveWorldXZ / (float)length : MoveWorldXZ;
    }
}
