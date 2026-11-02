using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Platformer.Core;

namespace Platformer.Tests.Regression;

/// <summary>
/// Независимые регрессии #9: эталоны выводятся из геометрии и дискретных формул
/// контракта Core v1, а не повторяют решатель BallSimulator.
/// </summary>
[TestClass]
public sealed class PhysicsRegressionTests
{
    // Допуск высоты прыжка: накопление float за ≤80 ticks порядка 1e-6; зазор опоры
    // SkinWidth не входит — подъём считается от начальной высоты того же шара.
    private const float PositionTolerance = 1e-4f;
    private const float NormalTolerance = 1e-5f;
    private static readonly SimulationSettings Defaults = SimulationSettings.Default;

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
            Assert.IsGreaterThanOrEqualTo(0.5f - Defaults.PenetrationSlop, state.Center.Y);
            Assert.IsLessThanOrEqualTo(0.5f + Defaults.SkinWidth + Defaults.PositionEpsilon, state.Center.Y);
            var initial = state;
            for (int tick = 0; tick < 120; tick++) state = simulator.Step(state, default).Ball;
            Near(initial.Center, state.Center, 1e-6f);
            Near(Vector3.Zero, state.Velocity, 1e-6f);
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
        var state = Air(surface + normal * (0.5f + Defaults.SkinWidth / 2f));

        state = simulator.Step(state, default).Ball;
        Assert.IsTrue(state.Grounded);
        Near(normal, state.GroundNormal, NormalTolerance);

        var uphill = new GameCommand(Vector2.UnitX, false, false);
        for (int tick = 0; tick < 40; tick++)
        {
            state = simulator.Step(state, uphill).Ball;
            Assert.IsTrue(state.Grounded, $"tick {tick}");
            Near(normal, state.GroundNormal, NormalTolerance);
        }
        // Скорость лежит в касательной плоскости: vy/vx = tan20°, длина не выше MoveSpeed.
        Assert.IsGreaterThan(0f, state.Velocity.X);
        Assert.AreEqual(MathF.Tan(angle), state.Velocity.Y / state.Velocity.X, 1e-4f);
        Assert.AreEqual(0f, state.Velocity.Z, 1e-6f);
        Assert.IsLessThanOrEqualTo(Defaults.MoveSpeed + 1e-5f, state.Velocity.Length());
        // Расстояние центра до плоскости рампы сохраняется: шар не отрывается и не тонет.
        float height = Vector3.Dot(state.Center - new Vector3(2f, 0f, 0f), normal);
        Assert.IsGreaterThanOrEqualTo(0.5f - Defaults.PenetrationSlop, height);
        Assert.IsLessThanOrEqualTo(0.5f + Defaults.SkinWidth + Defaults.PositionEpsilon, height);
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
        Vector3 spawn = normal * (0.2f + 0.5f + Defaults.SkinWidth / 2f);
        var simulator = new BallSimulator(Level(spawn, new SurfaceDefinition("slope", box)), Defaults);
        var state = simulator.CreateInitialState();
        Assert.AreEqual(expected, state.Grounded);
        if (expected) Near(normal, state.GroundNormal, NormalTolerance);
        else Assert.AreEqual(Vector3.Zero, state.GroundNormal);
    }

    [TestMethod]
    [DataRow(6f, 18f)]
    [DataRow(4.5f, 18f)]
    [DataRow(6f, 9f)]
    public void JumpApexMatchesClosedFormSemiImplicitEulerSum(float jumpSpeed, float gravity)
    {
        // Для default (6, 18, 1/120): N = 40, H = (240 − 123)/120 = 0.975.
        var settings = new SimulationSettings(jumpSpeed: jumpSpeed, gravity: gravity);
        double dt = settings.FixedStepSeconds;
        int risingTicks = (int)Math.Floor(jumpSpeed / (gravity * dt) + 1e-9);
        double apex = ClosedFormApex(jumpSpeed, gravity, dt);

        var simulator = new BallSimulator(Level(new Vector3(0f, 0.5005f, 0f), Floor()), settings);
        var initial = simulator.CreateInitialState();
        var state = simulator.Step(initial, new GameCommand(Vector2.Zero, true, false)).Ball;
        float highest = state.Center.Y;
        int landing = -1;
        for (int tick = 1; tick < 4 * risingTicks && landing < 0; tick++)
        {
            // Повторный фронт Space в воздухе не должен добавлять импульс.
            state = simulator.Step(state, new GameCommand(Vector2.Zero, true, false)).Ball;
            highest = MathF.Max(highest, state.Center.Y);
            if (state.Grounded) landing = tick;
        }
        Assert.AreEqual(apex, highest - initial.Center.Y, PositionTolerance);
        // Симметричная траектория: посадка примерно через 2N ticks.
        Assert.IsGreaterThanOrEqualTo(2 * risingTicks - 1, landing);
        Assert.IsLessThanOrEqualTo(2 * risingTicks + 2, landing);
        // Посадка возвращает центр в зазор опоры [r, r+SkinWidth], а не точно на высоту spawn.
        Assert.IsGreaterThanOrEqualTo(0.5f - settings.PenetrationSlop, state.Center.Y);
        Assert.IsLessThanOrEqualTo(0.5f + settings.SkinWidth + settings.PositionEpsilon, state.Center.Y);
    }

    [TestMethod]
    public void DefaultJumpApexIsExactlyPointNineSevenFive() =>
        Assert.AreEqual(0.975d, ClosedFormApex(6f, 18f, 1d / 120d), 1e-12d);

    [TestMethod]
    public void BoostPressEveryTickActivatesOncePerDurationPlusCooldownAndCapsSpeed()
    {
        // Фронт BoostPressed каждый tick 10 с (удержание клавиши — задача App/runner).
        // По контракту boost 1 с, затем cooldown 3 с, повторная активация только
        // с нулевых таймеров: ticks 0, 480, 960.
        var simulator = new BallSimulator(Level(new Vector3(-150f, 0.5005f, 0f), Floor(200f)), Defaults);
        var state = simulator.CreateInitialState();
        var held = new GameCommand(Vector2.UnitX, false, true);
        var activations = new List<int>();
        var speeds = new float[1200];
        float boostCap = Defaults.MoveSpeed * Defaults.BoostMultiplier;
        for (int tick = 0; tick < 1200; tick++)
        {
            var next = simulator.Step(state, held).Ball;
            if (state.BoostRemainingSeconds == 0f && next.BoostRemainingSeconds > 0f) activations.Add(tick);
            Assert.IsFalse(next.BoostRemainingSeconds > 0f && next.BoostCooldownSeconds > 0f, $"tick {tick}");
            speeds[tick] = Horizontal(next.Velocity);
            state = next;
        }
        CollectionAssert.AreEqual(new[] { 0, 480, 960 }, activations);
        Assert.AreEqual(boostCap, speeds[119], 1e-4f);
        Assert.AreEqual(Defaults.MoveSpeed, speeds[479], 1e-4f);
        Assert.IsLessThanOrEqualTo(boostCap + 1e-4f, speeds.Max());
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
                Assert.IsGreaterThanOrEqualTo(0.5f - Defaults.PenetrationSlop, state.Center.Y, $"phase {phase}, tick {tick}");
            }
            Assert.IsTrue(state.Grounded, $"phase {phase}");
            Assert.AreEqual(0.5f, state.Center.Y, Defaults.SkinWidth + Defaults.PositionEpsilon);
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
        // допустимым маршрутом, а не прямой хордой сквозь поверхность.
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
                        Assert.IsGreaterThanOrEqualTo(0.5f - Defaults.PenetrationSlop, distance, surface.Id);
                    }
                }
            }
            state = step.Ball;
        }
    }

    [TestMethod]
    public void EarlierResultsSurviveLaterStepsAndFailedTicks()
    {
        // Угол пола и стены с бюджетом 1 контакта: в одном tick шар сначала
        // касается пола (зазор 0.02), затем стены (0.04) — второй контакт превышает бюджет.
        var wall = new SurfaceDefinition("wall", new OrientedBox(new Vector3(1.04f, 0f, 0f),
            new Vector3(0.5f, 10f, 10f), Quaternion.Identity));
        var settings = new SimulationSettings(maxContactIterations: 1);
        var simulator = new BallSimulator(Level(new Vector3(-5f, 0.5005f, 0f), Floor(), wall), settings);
        var valid = simulator.CreateInitialState();
        var first = simulator.Step(valid, new GameCommand(Vector2.UnitX, false, false));
        var savedPath = first.Path.ToArray();
        var savedBall = first.Ball;

        var corner = Air(new Vector3(0f, 0.52f, 0f), new Vector3(8f, -4f, 0f));
        var failure = Assert.ThrowsExactly<InvalidOperationException>(() => simulator.Step(corner, default));
        StringAssert.Contains(failure.Message, "wall");
        Assert.AreEqual(Air(new Vector3(0f, 0.52f, 0f), new Vector3(8f, -4f, 0f)), corner);

        var state = first.Ball;
        for (int tick = 0; tick < 200; tick++) state = simulator.Step(state, default).Ball;
        Assert.AreEqual(savedBall, first.Ball);
        CollectionAssert.AreEqual(savedPath, first.Path.ToArray());
        Assert.ThrowsExactly<NotSupportedException>(() => ((IList<MotionSegment>)first.Path)[0] = default);
        // Ошибка не оставила скрытого состояния: повтор совпадает с первым результатом.
        var again = simulator.Step(valid, new GameCommand(Vector2.UnitX, false, false));
        Assert.AreEqual(first.Ball, again.Ball);
        CollectionAssert.AreEqual(savedPath, again.Path.ToArray());
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

    // Полунеявный Euler: v_n = v0 − n·g·dt, подъём пока v_n > 0; N = ⌊v0/(g·dt)⌋,
    // H = dt·(N·v0 − g·dt·N(N+1)/2).
    private static double ClosedFormApex(double jumpSpeed, double gravity, double dt)
    {
        int n = (int)Math.Floor(jumpSpeed / (gravity * dt) + 1e-9);
        return dt * (n * jumpSpeed - gravity * dt * n * (n + 1) / 2d);
    }

    private static SurfaceDefinition Floor(float half = 100f) =>
        new("floor", new OrientedBox(new Vector3(0f, -0.5f, 0f), new Vector3(half, 0.5f, half), Quaternion.Identity));

    private static LevelDefinition Level(Vector3 spawn, params SurfaceDefinition[] surfaces) =>
        new("regression", spawn, -50f, new OrientedBox(new Vector3(0f, 500f, 0f), Vector3.One, Quaternion.Identity),
            surfaces, [], []);

    private static BallState Air(Vector3 center, Vector3 velocity = default) =>
        new(center, velocity, Defaults.BallRadius, false, Vector3.Zero, 0f, 0f);

    private static float Horizontal(Vector3 velocity) => new Vector2(velocity.X, velocity.Z).Length();

    private static void Near(Vector3 expected, Vector3 actual, float tolerance) =>
        Assert.IsLessThanOrEqualTo(tolerance, Vector3.Distance(expected, actual), $"expected {expected}, actual {actual}");
}
