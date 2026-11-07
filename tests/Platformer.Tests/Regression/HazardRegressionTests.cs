using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Platformer.Core;
using static Platformer.Tests.Regression.RegressionFixtures;

namespace Platformer.Tests.Regression;

/// <summary>
/// Независимые регрессии #9 для вращающихся опасностей #20: поза из угла поворота,
/// swept-проверка неподвижного шара, контракт Path и бюджета.
/// </summary>
[TestClass]
public sealed class HazardRegressionTests
{
    // Балка с центром на радиусе 2 от оси Y занимает радиусы 1..3; самый дальний угол —
    // √(3²+0.1²+0.1²) ≈ 3.0033.
    private static readonly Vector3 Offset = new(2f, 0f, 0f);
    private static readonly Vector3 BeamHalf = new(1f, 0.1f, 0.1f);

    private static HazardDefinition Beam(float angularSpeed, float initialAngle, Vector3 pivot = default) =>
        new("beam", pivot, Vector3.UnitY, Offset, BeamHalf, Quaternion.Identity, angularSpeed, initialAngle);

    private static IReadOnlyList<MotionSegment> Stationary(Vector3 center) => [new(center, center, 0f, 1f)];

    [TestMethod]
    public void EvaluatePlacesCenterAtPivotPlusRotatedOffsetForAnyTime()
    {
        // Поворот вокруг +Y на θ в правой системе: X → (cosθ, 0, −sinθ).
        var pivot = new Vector3(1f, 2f, 3f);
        var hazard = Beam(2f, 0f, pivot);
        double quarter = Math.PI / 4d; // ω=2 → угол π/2
        double period = Math.PI;
        (double Time, Vector3 Center, Vector3 Axis)[] cases =
        [
            (0d, pivot + new Vector3(2f, 0f, 0f), Vector3.UnitX),
            (quarter, pivot + new Vector3(0f, 0f, -2f), -Vector3.UnitZ),
            (1e6d * period + quarter, pivot + new Vector3(0f, 0f, -2f), -Vector3.UnitZ),
        ];
        foreach (var (time, center, axis) in cases)
        {
            var pose = HazardMotion.Evaluate(hazard, time);
            Assert.AreEqual("beam", pose.Id);
            // Допуск большого времени: ulp(π·1e6)≈5e-10 c, ×ω и ×радиус 2 — порядка 1e-9;
            // остальное — округление float-кватерниона, далеко внутри 1e-4.
            Near(center, pose.Bounds.Center, AccumulatedTolerance);
            Near(axis, Vector3.Transform(Vector3.UnitX, pose.Bounds.Orientation), AccumulatedTolerance);
            Assert.AreEqual(BeamHalf, pose.Bounds.HalfExtents);
        }
        // Определение не меняется вычислением позы.
        Assert.AreEqual(pivot, hazard.Pivot);
        Assert.AreEqual(Offset, hazard.CenterOffset);
        Assert.AreEqual(0f, hazard.InitialAngle);
        Assert.AreEqual(2f, hazard.AngularSpeed);
    }

    [TestMethod]
    public void BeamSweepingThroughStationaryBallWithinOneTickIsAHit()
    {
        // Шар на радиусе 2 по +X. За tick балка поворачивается с −30° до +30°: в обеих
        // позах расстояние от центра шара до оси балки 2·sin30° = 1 больше r+0.1, а в
        // середине tick балка проходит прямо через шар. Проверка только по endpoints
        // такой удар пропустила бы.
        float tick = Defaults.FixedStepSeconds;
        float sweep = MathF.PI / 3f;
        var ball = new Vector3(2f, 0f, 0f);
        var hazard = Beam(sweep / tick, -sweep / 2f);
        foreach (double time in new[] { 0d, tick })
        {
            var bounds = HazardMotion.Evaluate(hazard, time).Bounds;
            Assert.IsGreaterThan(Radius, Vector3.Distance(ball, bounds.ClosestPoint(ball)), $"t={time}");
        }
        Assert.IsTrue(HazardCollision.IntersectsPath(hazard, Stationary(ball), 0d, Defaults));
    }

    [TestMethod]
    public void SessionFailsWhenBeamSweepsThroughRestingBallInFirstTick()
    {
        // Та же геометрия через GameSession: шар покоится на полу, ось балки на высоте его
        // центра; ни поза до tick, ни поза после него шар не задевают.
        float sweep = MathF.PI / 3f;
        var hazard = Beam(sweep / Defaults.FixedStepSeconds, -sweep / 2f, new Vector3(0f, 0.5f, 0f));
        var level = new LevelDefinition("regression", new Vector3(2f, 0.501f, 0f), -3f,
            new OrientedBox(new Vector3(0f, 500f, 0f), Vector3.One, Quaternion.Identity), [Floor()], [], [hazard]);
        var session = new GameSession(level, Defaults);
        var ball = session.Snapshot.Ball.Center;
        Assert.IsGreaterThan(Radius, Vector3.Distance(ball, session.Snapshot.Hazards.Single().Bounds.ClosestPoint(ball)));

        session.Step(default);
        ball = session.Snapshot.Ball.Center;
        Assert.IsGreaterThan(Radius, Vector3.Distance(ball, session.Snapshot.Hazards.Single().Bounds.ClosestPoint(ball)));
        Assert.AreEqual(SessionPhase.Failed, session.Snapshot.Phase);
        Assert.AreEqual(1L, session.DrainEvents().Single(e => e.Kind == GameEventKind.Failed).Tick);
    }

    [TestMethod]
    public void BeamThatStopsShortOrPassesBeyondReachDoesNotHit()
    {
        float tick = Defaults.FixedStepSeconds;
        // Поворот −30°→−20°: ось балки остаётся в 2·sin20° ≈ 0.68 > r+0.1·√2 от центра.
        var shortSweep = Beam((MathF.PI / 18f) / tick, -MathF.PI / 6f);
        Assert.IsFalse(HazardCollision.IntersectsPath(shortSweep, Stationary(new Vector3(2f, 0f, 0f)), 0d, Defaults));
        // Полный проход −30°→+30°, но шар на радиусе 3.6: зазор 3.6 − r − 3.0033 ≈ 0.097.
        var fullSweep = Beam((MathF.PI / 3f) / tick, -MathF.PI / 6f);
        Assert.IsFalse(HazardCollision.IntersectsPath(fullSweep, Stationary(new Vector3(3.6f, 0f, 0f)), 0d, Defaults));
    }

    [TestMethod]
    public void BrokenPathIsRejectedAndExhaustedBudgetNamesHazard()
    {
        var hazard = Beam(1f, 0f, new Vector3(100f, 0f, 0f));
        var a = Vector3.Zero;
        var b = new Vector3(0.01f, 0f, 0f);
        IReadOnlyList<MotionSegment>[] broken =
        [
            [],
            [new(a, b, 0f, 0.4f), new(b, b, 0.5f, 1f)],                    // зазор долей
            [new(a, b, 0f, 0.5f), new(b + Vector3.UnitY, b, 0.5f, 1f)],    // разрыв End→Start
            [new(a, b, 0f, 0.9f)],                                         // не покрывает [0,1]
            [new(b, a, 0.5f, 1f), new(a, b, 0f, 0.5f)],                    // обратный порядок
        ];
        foreach (var path in broken)
            Assert.ThrowsExactly<ArgumentException>(() => HazardCollision.IntersectsPath(hazard, path, 0d, Defaults));

        // Отрезок 10 при шаге проверки r/4 = 0.125 требует 80 подшагов > 32.
        IReadOnlyList<MotionSegment> far = [new(a, new Vector3(10f, 0f, 0f), 0f, 1f)];
        var failure = Assert.ThrowsExactly<InvalidOperationException>(() =>
            HazardCollision.IntersectsPath(hazard, far, 0d, Defaults));
        StringAssert.Contains(failure.Message, "beam");
    }

    [TestMethod]
    public void TurningMazeArenaBeamReachesBallRestingInArenaWithinGeometricWindow()
    {
        // Реальные данные level2: балка ±2 вдоль своей оси, полутолщина 0.15, ω=1, старт вдоль X.
        // Шар покоится на радиусе 1.5 по −Z, т.е. под углом π/2 от начальной оси.
        // Раньше θ₁ = acos((r+0.15√2)/1.5) контакт невозможен; к θ₂ = acos(r/1.5) ось
        // балки уже проходит через шар. Вертикально балка 0.4..0.7 пересекает шар 0..1.
        var maze = TurningMaze.Create();
        var beam = maze.Hazards.Single();
        Assert.AreEqual(1f, beam.AngularSpeed);
        Assert.AreEqual(0f, beam.InitialAngle);
        var spawn = beam.Pivot with { Y = 0.501f } - Vector3.UnitZ * 1.5f;
        var level = new LevelDefinition(maze.Id, spawn, maze.KillY, maze.Finish, maze.Surfaces, [], maze.Hazards);
        var session = new GameSession(level, Defaults);
        int limit = (int)(2d * Math.PI / Defaults.FixedStepSeconds);
        while (session.Snapshot.Phase == SessionPhase.Running && session.Snapshot.Tick < limit)
            session.Step(default);

        Assert.AreEqual(SessionPhase.Failed, session.Snapshot.Phase);
        double hitAngle = session.Snapshot.SimulationSeconds * beam.AngularSpeed;
        Assert.IsGreaterThanOrEqualTo(Math.Acos((Radius + 0.15 * Math.Sqrt(2d)) / 1.5), hitAngle);
        Assert.IsLessThanOrEqualTo(Math.Acos(Radius / 1.5) + Defaults.FixedStepSeconds, hitAngle);
        var events = session.DrainEvents();
        Assert.AreEqual(GameEventKind.Failed, events.Single().Kind);
    }
}
