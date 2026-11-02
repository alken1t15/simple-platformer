using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Platformer.Core;
using static Platformer.Tests.Regression.RegressionFixtures;

namespace Platformer.Tests.Regression;

/// <summary>
/// Независимые регрессии #9: эталоны выводятся из геометрии и дискретных формул
/// контракта Core v1, а не повторяют решатель BallSimulator.
/// </summary>
[TestClass]
public sealed class PhysicsRegressionTests
{
    [TestMethod]
    public void FlatTopFaceGivesUpNormalForAnyEquivalentBoxOrientation()
    {
        // Одна и та же плита толщиной 1 с верхней гранью y=0, заданная разными
        // local-осями: поворот вокруг Y, ось X→Y (90° вокруг Z) и переворот на 180° вокруг X.
        var plates = new[]
        {
            new OrientedBox(new Vector3(0f, -0.5f, 0f), new Vector3(10f, 0.5f, 10f),
                Quaternion.CreateFromAxisAngle(Vector3.UnitY, 0.7f)),
            new OrientedBox(new Vector3(0f, -0.5f, 0f), new Vector3(0.5f, 10f, 10f),
                Quaternion.CreateFromAxisAngle(Vector3.UnitZ, MathF.PI / 2f)),
            new OrientedBox(new Vector3(0f, -0.5f, 0f), new Vector3(10f, 0.5f, 10f),
                Quaternion.CreateFromAxisAngle(Vector3.UnitX, MathF.PI)),
        };
        foreach (var plate in plates)
        {
            var simulator = new BallSimulator(Level(new Vector3(0.3f, 0.5005f, -0.2f), new SurfaceDefinition("plate", plate)), Defaults);
            var state = simulator.CreateInitialState();
            Assert.IsTrue(state.Grounded);
            Near(Vector3.UnitY, state.GroundNormal, NormalTolerance);
            AssertWithinSupportGap(state.Center.Y);
            var initial = state;
            for (int tick = 0; tick < 120; tick++) state = simulator.Step(state, default).Ball;
            Near(initial.Center, state.Center, DriftTolerance);
            Near(Vector3.Zero, state.Velocity, DriftTolerance);
        }
    }

    [TestMethod]
    public void TrainingTrackRampNormalAndUphillDirectionFollowTwentyDegreeGeometry()
    {
        // Из раскладки docs/training-track.md: верх рампы идёт от (2,0,0) к (5,3·tan20°,0).
        float angle = 20f * MathF.PI / 180f;
        var normal = new Vector3(-MathF.Sin(angle), MathF.Cos(angle), 0f);
        var simulator = new BallSimulator(TrainingTrack.Create(), Defaults);
        float x = 2.6f;
        var surface = new Vector3(x, (x - 2f) * MathF.Tan(angle), 0f);
        var state = Air(surface + normal * (Radius + Defaults.SkinWidth / 2f));

        state = simulator.Step(state, default).Ball;
        Assert.IsTrue(state.Grounded);
        Near(normal, state.GroundNormal, NormalTolerance);

        for (int tick = 0; tick < 40; tick++)
        {
            state = simulator.Step(state, Right).Ball;
            Assert.IsTrue(state.Grounded, $"tick {tick}");
            Near(normal, state.GroundNormal, NormalTolerance);
        }
        // Скорость лежит в касательной плоскости: vy/vx = tan20°, длина не выше MoveSpeed.
        Assert.IsGreaterThan(0f, state.Velocity.X);
        Assert.AreEqual(MathF.Tan(angle), state.Velocity.Y / state.Velocity.X, AccumulatedTolerance);
        Assert.AreEqual(0f, state.Velocity.Z, DriftTolerance);
        Assert.IsLessThanOrEqualTo(Defaults.MoveSpeed + AccumulatedTolerance, state.Velocity.Length());
        // Расстояние центра до плоскости рампы сохраняется: шар не отрывается и не тонет.
        AssertWithinSupportGap(Vector3.Dot(state.Center - new Vector3(2f, 0f, 0f), normal));
    }

    [TestMethod]
    [DataRow(49f, true)]
    [DataRow(51f, false)]
    public void SupportFlipsAroundConfiguredMaxSlope(float degrees, bool expected)
    {
        // MaxSlope=50°: граница проверяется с обеих сторон на ±1°.
        float angle = degrees * MathF.PI / 180f;
        var rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, angle);
        var box = new OrientedBox(Vector3.Zero, new Vector3(20f, 0.2f, 20f), rotation);
        Vector3 normal = new(-MathF.Sin(angle), MathF.Cos(angle), 0f);
        Vector3 spawn = normal * (0.2f + Radius + Defaults.SkinWidth / 2f);
        var simulator = new BallSimulator(Level(spawn, new SurfaceDefinition("slope", box)), Defaults);
        var state = simulator.CreateInitialState();
        Assert.AreEqual(expected, state.Grounded);
        if (expected) Near(normal, state.GroundNormal, NormalTolerance);
        else Assert.AreEqual(Vector3.Zero, state.GroundNormal);
    }

    [TestMethod]
    [DataRow(6f, 18f, 40, 0.975)]
    [DataRow(4.5f, 18f, 30, 0.54375)]
    [DataRow(6f, 9f, 80, 1.975)]
    public void JumpApexMatchesClosedFormSemiImplicitEulerSum(float jumpSpeed, float gravity, int risingTicks, double apex)
    {
        // Эталоны посчитаны вручную. Полунеявный Euler с dt=1/120: v_n = v0 − n·g·dt,
        // подъём пока v_n > 0, N = v0/(g·dt), H = dt·(N·v0 − g·dt·N(N+1)/2).
        // Default (6, 18): N = 40, H = (240 − 123)/120 = 0.975; (4.5, 18): (135 − 69.75)/120;
        // (6, 9): (480 − 243)/120.
        var settings = new SimulationSettings(jumpSpeed: jumpSpeed, gravity: gravity);
        var simulator = new BallSimulator(Level(new Vector3(0f, 0.5005f, 0f), Floor()), settings);
        var initial = simulator.CreateInitialState();
        var jump = new GameCommand(Vector2.Zero, true, false);
        var state = simulator.Step(initial, jump).Ball;
        float highest = state.Center.Y;
        int landing = -1;
        for (int tick = 1; tick < 4 * risingTicks && landing < 0; tick++)
        {
            // Повторный фронт Space в воздухе не должен добавлять импульс.
            state = simulator.Step(state, jump).Ball;
            highest = MathF.Max(highest, state.Center.Y);
            if (state.Grounded) landing = tick;
        }
        // Подъём считается от начальной высоты того же шара, зазор опоры в допуск не входит.
        Assert.AreEqual(apex, highest - initial.Center.Y, AccumulatedTolerance);
        // Возврат на исходную высоту: Σv_k = n·v0 − g·dt·n(n+1)/2 = 0 при n = 2N − 1 шагов;
        // landing — индекс шага с нуля, т.е. 2N − 2, ±1 на зазор опоры.
        Assert.IsGreaterThanOrEqualTo(2 * risingTicks - 3, landing);
        Assert.IsLessThanOrEqualTo(2 * risingTicks - 1, landing);
        // Посадка возвращает центр в зазор опоры, а не точно на высоту spawn.
        AssertWithinSupportGap(state.Center.Y);
    }

    [TestMethod]
    public void BoostPressEveryTickActivatesOncePerDurationPlusCooldownAndCapsSpeed()
    {
        // Фронт BoostPressed каждый tick 10 с (удержание клавиши — задача App/runner).
        // По контракту boost 1 с, затем cooldown 3 с, повторная активация только
        // с нулевых таймеров: ticks 0, 480, 960.
        var simulator = new BallSimulator(Level(new Vector3(-150f, 0.5005f, 0f), Floor(200f)), Defaults);
        var state = simulator.CreateInitialState();
        var pressEveryTick = new GameCommand(Vector2.UnitX, false, true);
        var activations = new List<int>();
        var speeds = new float[1200];
        float boostCap = Defaults.MoveSpeed * Defaults.BoostMultiplier;
        for (int tick = 0; tick < 1200; tick++)
        {
            var next = simulator.Step(state, pressEveryTick).Ball;
            if (state.BoostRemainingSeconds == 0f && next.BoostRemainingSeconds > 0f) activations.Add(tick);
            Assert.IsFalse(next.BoostRemainingSeconds > 0f && next.BoostCooldownSeconds > 0f, $"tick {tick}");
            speeds[tick] = Horizontal(next.Velocity);
            state = next;
        }
        CollectionAssert.AreEqual(new[] { 0, 480, 960 }, activations);
        Assert.AreEqual(boostCap, speeds[119], AccumulatedTolerance);
        Assert.AreEqual(Defaults.MoveSpeed, speeds[479], AccumulatedTolerance);
        Assert.IsLessThanOrEqualTo(boostCap + AccumulatedTolerance, speeds.Max());
    }

    [TestMethod]
    public void MaxFallSpeedNeverTunnelsThroughThinPlateAtAnyTickPhase()
    {
        // Плита толщиной 0.002 и максимальная скорость падения 30: 0.25 за tick.
        // 37 сдвигов старта покрывают фазы относительно грани с шагом ≈0.0068.
        var plate = new SurfaceDefinition("thin", new OrientedBox(new Vector3(0f, -0.001f, 0f),
            new Vector3(5f, 0.001f, 5f), Quaternion.Identity));
        var simulator = new BallSimulator(Level(new Vector3(0f, 10f, 0f), plate), Defaults);
        for (int phase = 0; phase < 37; phase++)
        {
            var state = Air(new Vector3(0f, 3f + phase * 0.25f / 37f, 0f), new Vector3(0f, -Defaults.MaxFallSpeed, 0f));
            for (int tick = 0; tick < 30 && !state.Grounded; tick++)
            {
                state = simulator.Step(state, default).Ball;
                Assert.IsGreaterThanOrEqualTo(Radius - Defaults.PenetrationSlop, state.Center.Y, $"phase {phase}, tick {tick}");
            }
            Assert.IsTrue(state.Grounded, $"phase {phase}");
            AssertWithinSupportGap(state.Center.Y, $"phase {phase}");
        }
    }

    [TestMethod]
    public void TrainingTrackRouteReplayIsBitIdenticalForAnySurfaceOrder()
    {
        // Полный повтор одной записи команд: новый симулятор, повтор на том же
        // экземпляре и уровни с переставленными поверхностями дают одинаковые ticks.
        var track = TrainingTrack.Create();
        var commands = RouteCommands();
        var reference = Replay(new BallSimulator(track, Defaults), commands);
        var sameInstance = new BallSimulator(track, Defaults);
        Replay(sameInstance, commands);
        AssertSameReplay(reference, Replay(sameInstance, commands));

        var random = new Random(9);
        var orders = new List<SurfaceDefinition[]> { track.Surfaces.Reverse().ToArray() };
        for (int i = 0; i < 3; i++) orders.Add(track.Surfaces.OrderBy(_ => random.Next()).ToArray());
        foreach (var shuffled in orders)
        {
            var permuted = new LevelDefinition(track.Id, track.Spawn, track.KillY, track.Finish,
                shuffled, track.Coins, track.Hazards);
            AssertSameReplay(reference, Replay(new BallSimulator(permuted, Defaults), commands));
        }
    }

    [TestMethod]
    public void RoutePathIsContinuousCoversWholeTickAndNeverEntersSurfaces()
    {
        // Path — вход для монет/финиша/опасностей (#8, #20): он должен быть физически
        // допустимым маршрутом, а не прямой хордой сквозь поверхность. В 9 точках каждого
        // отрезка сфера не проникает в OBB глубже PenetrationSlop.
        var track = TrainingTrack.Create();
        var simulator = new BallSimulator(track, Defaults);
        var state = simulator.CreateInitialState();
        foreach (var command in RouteCommands())
        {
            var step = simulator.Step(state, command);
            Assert.IsNotEmpty(step.Path);
            Assert.AreEqual(0f, step.Path[0].StartFraction);
            Assert.AreEqual(1f, step.Path[^1].EndFraction);
            Near(state.Center, step.Path[0].Start, Defaults.PositionEpsilon);
            Near(step.Ball.Center, step.Path[^1].End, Defaults.PositionEpsilon);
            for (int i = 0; i < step.Path.Count; i++)
            {
                var segment = step.Path[i];
                Assert.IsGreaterThan(segment.StartFraction, segment.EndFraction);
                if (i > 0)
                {
                    Assert.AreEqual(step.Path[i - 1].EndFraction, segment.StartFraction);
                    Near(step.Path[i - 1].End, segment.Start, Defaults.PositionEpsilon);
                }
                for (int sample = 0; sample <= 8; sample++)
                {
                    Vector3 point = Vector3.Lerp(segment.Start, segment.End, sample / 8f);
                    foreach (var surface in track.Surfaces)
                    {
                        float distance = Vector3.Distance(point, surface.Bounds.ClosestPoint(point));
                        Assert.IsGreaterThanOrEqualTo(Radius - Defaults.PenetrationSlop, distance, surface.Id);
                    }
                }
            }
            state = step.Ball;
        }
    }

    [TestMethod]
    public void FailedTickLeavesNoHiddenStateAndEarlierPathsStayIntact()
    {
        // Угол пола и стены с бюджетом 1 контакта: в одном tick шар сначала
        // касается пола (зазор 0.02), затем стены (0.04) — второй контакт превышает бюджет.
        // BallState — значение, поэтому проверяется наблюдаемое: Path прежних результатов
        // и отсутствие состояния внутри симулятора после ошибки.
        var wall = new SurfaceDefinition("wall", new OrientedBox(new Vector3(1.04f, 0f, 0f),
            new Vector3(0.5f, 10f, 10f), Quaternion.Identity));
        var settings = new SimulationSettings(maxContactIterations: 1);
        var simulator = new BallSimulator(Level(new Vector3(-5f, 0.5005f, 0f), Floor(), wall), settings);
        var valid = simulator.CreateInitialState();
        var first = simulator.Step(valid, Right);
        var savedPath = first.Path.ToArray();

        var corner = Air(new Vector3(0f, 0.52f, 0f), new Vector3(8f, -4f, 0f));
        var failure = Assert.ThrowsExactly<InvalidOperationException>(() => simulator.Step(corner, default));
        StringAssert.Contains(failure.Message, "wall");

        var state = first.Ball;
        for (int tick = 0; tick < 200; tick++) state = simulator.Step(state, default).Ball;
        CollectionAssert.AreEqual(savedPath, first.Path.ToArray());
        Assert.ThrowsExactly<NotSupportedException>(() => ((IList<MotionSegment>)first.Path)[0] = default);
        var again = simulator.Step(valid, Right);
        Assert.AreEqual(first.Ball, again.Ball);
        CollectionAssert.AreEqual(savedPath, again.Path.ToArray());
    }

    [TestMethod]
    public void SimultaneousContactBudgetErrorNamesSameSurfaceForAnyOrder()
    {
        // Пол (верх y=0) и стена (грань x=0) симметричны относительно диагонали. Числа
        // точно представимы: dt=1/64, g=16, без air control, поэтому после гравитации
        // скорость ровно (8, −8) и оба контакта приходятся на одну долю tick. При бюджете 1
        // ошибка называет вторую поверхность; она не зависит от порядка списка, потому что
        // равенство разрешается ordinal Surface.Id: первым идёт floor, ошибка — на wall.
        var wall = new SurfaceDefinition("wall", new OrientedBox(new Vector3(0.5f, 0f, 0f),
            new Vector3(0.5f, 100f, 100f), Quaternion.Identity));
        var settings = new SimulationSettings(fixedStepSeconds: 1f / 64f, gravity: 16f,
            airControlFactor: 0f, maxContactIterations: 1);
        var corner = Air(new Vector3(-0.55f, 0.55f, 0f), new Vector3(8f, -7.75f, 0f));
        string Failure(params SurfaceDefinition[] surfaces) =>
            Assert.ThrowsExactly<InvalidOperationException>(() =>
                new BallSimulator(Level(new Vector3(-5f, 0.5005f, 0f), surfaces), settings).Step(corner, default)).Message;
        string expected = Failure(Floor(), wall);
        StringAssert.Contains(expected, "'wall'");
        Assert.AreEqual(expected, Failure(wall, Floor()));
    }

    private static List<GameCommand> RouteCommands()
    {
        // Маршрут Training Track: разгон, подъём по рампе, прыжки, boost и торможение.
        var commands = new List<GameCommand>();
        void Hold(int ticks, Vector2 move, bool jumpOnFirstTick = false, bool boostOnFirstTick = false)
        {
            for (int i = 0; i < ticks; i++)
                commands.Add(new GameCommand(move, jumpOnFirstTick && i == 0, boostOnFirstTick && i == 0));
        }
        Hold(150, Vector2.UnitX);
        Hold(30, Vector2.Zero, jumpOnFirstTick: true);
        Hold(90, Vector2.UnitY);
        Hold(60, Vector2.Normalize(new Vector2(-1f, 1f)), boostOnFirstTick: true);
        Hold(40, Vector2.Zero);
        return commands;
    }

    private static List<BallStepResult> Replay(BallSimulator simulator, List<GameCommand> commands)
    {
        var results = new List<BallStepResult>();
        var state = simulator.CreateInitialState();
        foreach (var command in commands)
        {
            var step = simulator.Step(state, command);
            results.Add(step);
            state = step.Ball;
        }
        return results;
    }

    private static void AssertSameReplay(List<BallStepResult> expected, List<BallStepResult> actual)
    {
        Assert.HasCount(expected.Count, actual);
        for (int i = 0; i < expected.Count; i++)
        {
            Assert.AreEqual(expected[i].Ball, actual[i].Ball, $"tick {i}");
            CollectionAssert.AreEqual(expected[i].Path.ToArray(), actual[i].Path.ToArray(), $"tick {i}");
        }
    }

    private static float Horizontal(Vector3 velocity) => new Vector2(velocity.X, velocity.Z).Length();
}
