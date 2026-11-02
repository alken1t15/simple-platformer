using System.Numerics;

namespace Platformer.Core;

/// <summary>Значение состояния шара; GroundNormal равна нулю без опоры.</summary>
public readonly record struct BallState(
    Vector3 Center, Vector3 Velocity, float Radius, bool Grounded, Vector3 GroundNormal,
    float BoostRemainingSeconds, float BoostCooldownSeconds);
