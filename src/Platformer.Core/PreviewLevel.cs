using System.Numerics;

namespace Platformer.Core;

/// <summary>Геометрия одной платформы; Size задаёт полные размеры по X/Y/Z.</summary>
public readonly record struct Platform(Vector3 Center, Vector3 Size);

/// <summary>Технический макет уровня для проверки интеграции, не финальный игровой дизайн.</summary>
public static class PreviewLevel
{
    public static IReadOnlyList<Platform> Platforms { get; } = Array.AsReadOnly<Platform>(
    [
        new(new Vector3(0f, -0.5f, 0f), new Vector3(6f, 1f, 3f)),
        new(new Vector3(4f, 0.5f, 0f), new Vector3(2f, 1f, 3f)),
        new(new Vector3(7f, 1.5f, 0f), new Vector3(2f, 1f, 3f)),
    ]);
}
