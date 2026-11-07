using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Platformer.Core;

namespace Platformer.Tests.Regression;

/// <summary>
/// Общие данные и допуски регрессий #9. Намеренно отделены от хелперов авторских
/// тестов, чтобы независимые проверки не наследовали их предположения.
/// </summary>
internal static class RegressionFixtures
{
    internal static readonly SimulationSettings Defaults = SimulationSettings.Default;
    internal static readonly float Radius = Defaults.BallRadius;
    internal static readonly GameCommand Right = new(Vector2.UnitX, false, false);

    // Нормаль строится из нормализованного float-кватерниона и OBB-преобразования:
    // ошибка — несколько ULP единичного вектора (~1e-7), запас ×100.
    internal const float NormalTolerance = 1e-5f;
    // Высота/скорость после ≤1200 ticks: накопление float при модулях ≤10 порядка 1e-6..1e-5;
    // запас на порядок, но много меньше SkinWidth=1e-3 и шага скорости за tick (1/6).
    internal const float AccumulatedTolerance = 1e-4f;
    // Покой без команды: Approach к нулевой цели даёт точный ноль, остаётся только
    // округление позиции около единицы.
    internal const float DriftTolerance = 1e-6f;

    internal static SurfaceDefinition Floor(float half = 100f) =>
        new("floor", new OrientedBox(new Vector3(0f, -0.5f, 0f), new Vector3(half, 0.5f, half), Quaternion.Identity));

    internal static LevelDefinition Level(Vector3 spawn, params SurfaceDefinition[] surfaces) =>
        new("regression", spawn, -50f, new OrientedBox(new Vector3(0f, 500f, 0f), Vector3.One, Quaternion.Identity),
            surfaces, [], []);

    internal static BallState Air(Vector3 center, Vector3 velocity = default) =>
        new(center, velocity, Radius, false, Vector3.Zero, 0f, 0f);

    /// <summary>Отпускает шар из <paramref name="start"/> и ждёт покоя на опоре; иначе тест падает.</summary>
    internal static BallState DropOnto(BallSimulator simulator, Vector3 start, string label)
    {
        var state = Air(start);
        for (int tick = 0; tick < 240; tick++)
        {
            state = simulator.Step(state, default).Ball;
            if (state.Grounded && state.Velocity.LengthSquared() < 1e-8f) return state;
        }
        Assert.Fail($"{label}: шар не нашёл опору под точкой");
        return state;
    }

    /// <summary>Расстояние центра до опорной плоскости лежит в зазоре [r−PenetrationSlop, r+SkinWidth+PositionEpsilon].</summary>
    internal static void AssertWithinSupportGap(float height, string message = "")
    {
        Assert.IsGreaterThanOrEqualTo(Radius - Defaults.PenetrationSlop, height, message);
        Assert.IsLessThanOrEqualTo(Radius + Defaults.SkinWidth + Defaults.PositionEpsilon, height, message);
    }

    internal static void Near(Vector3 expected, Vector3 actual, float tolerance) =>
        Assert.IsLessThanOrEqualTo(tolerance, Vector3.Distance(expected, actual), $"expected {expected}, actual {actual}");
}
