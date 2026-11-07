using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Platformer.Core;
using static Platformer.Tests.Regression.RegressionFixtures;

namespace Platformer.Tests.Regression;

/// <summary>
/// Независимые регрессии #9 для GameSession (#8) с опасностями (#20): триггеры по Path,
/// приоритет поражения, Restart и смена SessionId, атомарность ошибки, порядок данных.
/// Места триггеров вычисляются из Path опорного прогона, а не из формул решателя.
/// </summary>
[TestClass]
public sealed class SessionRegressionTests
{
    private const int ProbeTick = 100;
    private static readonly GameCommand Left = new(-Vector2.UnitX, false, false);
    private static readonly OrientedBox FarFinish = Box(new Vector3(500f, 0.5f, 0f), new Vector3(0.5f));

    private static OrientedBox Box(Vector3 center, Vector3 half) => new(center, half, Quaternion.Identity);

    private static LevelDefinition Arena(OrientedBox? finish = null, IReadOnlyList<CoinDefinition>? coins = null,
        IReadOnlyList<HazardDefinition>? hazards = null, params SurfaceDefinition[] extra) =>
        new("regression", new Vector3(0f, 0.501f, 0f), -3f, finish ?? FarFinish,
            [Floor(50f), .. extra], coins ?? [], hazards ?? []);

    private static HazardDefinition StaticHazard(string id, Vector3 center, Vector3 half) =>
        new(id, center, Vector3.UnitY, Vector3.Zero, half, Quaternion.Identity, 0f, 0f);

    /// <summary>Path tick-а <paramref name="tick"/> при беге вправо по пустому полу.</summary>
    private static IReadOnlyList<MotionSegment> ReferencePath(int tick)
    {
        var session = new GameSession(Arena(), Defaults);
        for (int i = 0; i < tick; i++) session.Step(Right);
        Assert.AreEqual(SessionPhase.Running, session.Snapshot.Phase);
        return session.LastPath;
    }

    private static List<GameEvent> RunRight(GameSession session, int ticks)
    {
        var events = new List<GameEvent>();
        for (int i = 0; i < ticks; i++)
        {
            session.Step(Right);
            events.AddRange(session.DrainEvents());
        }
        return events;
    }

    [TestMethod]
    public void ThinCoinBesideTickChordIsCollectedByPathBetweenEndpoints()
    {
        // Монета сбоку от середины отрезка tick на R−ε, R = r + coinR. Концы отрезка
        // отстоят на √((R−ε)² + (L/2)²): при ε = L²/(32R) это R + ~3L²/(32R) > R, т.е. ни
        // одна поза шара монету не касается, касается только его путь внутри tick.
        var path = ReferencePath(ProbeTick);
        Assert.HasCount(1, path, "на ровном полу tick — один отрезок");
        var (start, end) = (path[0].Start, path[0].End);
        float length = Vector3.Distance(start, end);
        Assert.IsGreaterThan(0.03f, length);
        const float coinRadius = 0.05f;
        float reach = Radius + coinRadius;
        float epsilon = length * length / (32f * reach);
        var middle = (start + end) / 2f;

        var inside = new CoinDefinition("thin", middle + Vector3.UnitZ * (reach - epsilon), coinRadius);
        Assert.IsGreaterThan(reach, Vector3.Distance(start, inside.Center));
        Assert.IsGreaterThan(reach, Vector3.Distance(end, inside.Center));
        var events = RunRight(new GameSession(Arena(coins: [inside]), Defaults), ProbeTick + 50);
        var collected = events.Single(e => e.Kind == GameEventKind.CoinCollected);
        Assert.AreEqual("thin", collected.EntityId);
        Assert.AreEqual(ProbeTick, collected.Tick);

        var outside = new CoinDefinition("thin", middle + Vector3.UnitZ * (reach + 1e-3f), coinRadius);
        var missed = RunRight(new GameSession(Arena(coins: [outside]), Defaults), ProbeTick + 50);
        Assert.IsEmpty(missed);
    }

    [TestMethod]
    public void CoinOnPathCountsOnceEvenWhenBallPassesItAgain()
    {
        var coin = new CoinDefinition("coin", new Vector3(1.5f, 0.5f, 0f), 0.2f);
        var session = new GameSession(Arena(coins: [coin]), Defaults);
        var events = new List<GameEvent>();
        foreach (var (command, ticks) in new[] { (Right, 120), (Left, 240), (Right, 240) })
            for (int i = 0; i < ticks; i++)
            {
                session.Step(command);
                events.AddRange(session.DrainEvents());
            }
        Assert.AreEqual(SessionPhase.Running, session.Snapshot.Phase);
        Assert.AreEqual("coin", events.Single(e => e.Kind == GameEventKind.CoinCollected).EntityId);
        CollectionAssert.AreEqual(new[] { "coin" }, session.Snapshot.CollectedCoinIds.ToArray());
    }

    [TestMethod]
    public void HazardTouchedInSameTickAsFinishAndCoinFailsWithoutCompletingOrCollecting()
    {
        // Все три объекта ставятся так, что конец Path tick-а K входит в них на δ = L/2,
        // а начало (конец K−1) отстоит на L/2 снаружи: касание только в tick K.
        var path = ReferencePath(ProbeTick);
        var (start, end) = (path[0].Start, path[^1].End);
        float delta = Vector3.Distance(start, end) / 2f;
        float face = end.X + Radius - delta;
        var finish = Box(new Vector3(face + 0.5f, end.Y, 0f), new Vector3(0.5f, 1f, 1f));
        var coin = new CoinDefinition("coin", new Vector3(face + 0.2f, end.Y, 0f), 0.2f);
        var hazard = StaticHazard("wall", new Vector3(face + 0.5f, end.Y, 0f), new Vector3(0.5f, 0.2f, 0.2f));

        // Контроль: без опасности в tick K засчитываются и монета, и финиш.
        var control = new GameSession(Arena(finish, [coin]), Defaults);
        var controlEvents = RunRight(control, ProbeTick);
        Assert.AreEqual(SessionPhase.LevelComplete, control.Snapshot.Phase);
        CollectionAssert.AreEquivalent(new[] { GameEventKind.CoinCollected, GameEventKind.LevelCompleted },
            controlEvents.Select(e => e.Kind).ToArray());
        Assert.IsTrue(controlEvents.All(e => e.Tick == ProbeTick));

        var session = new GameSession(Arena(finish, [coin], [hazard]), Defaults);
        var events = RunRight(session, ProbeTick - 1);
        Assert.IsEmpty(events);
        Assert.AreEqual(SessionPhase.Running, session.Snapshot.Phase);
        events = RunRight(session, 10);
        Assert.AreEqual(SessionPhase.Failed, session.Snapshot.Phase);
        Assert.AreEqual(ProbeTick, session.Snapshot.Tick, "после Failed сеанс не продвигается");
        var failed = events.Single();
        Assert.AreEqual(GameEventKind.Failed, failed.Kind);
        Assert.AreEqual(ProbeTick, failed.Tick);
        Assert.AreEqual(0, session.Snapshot.CoinCount);
    }

    [TestMethod]
    public void RestartIssuesFreshSessionAndLeavesEarlierSnapshotUntouched()
    {
        var coin = new CoinDefinition("coin", new Vector3(1f, 0.5f, 0f), 0.2f);
        var spinner = new HazardDefinition("spinner", new Vector3(0f, 0.5f, 30f), Vector3.UnitY,
            new Vector3(1f, 0f, 0f), new Vector3(0.5f, 0.1f, 0.1f), Quaternion.Identity, 1f, 0f);
        var session = new GameSession(Arena(coins: [coin], hazards: [spinner]), Defaults);
        session.Step(new GameCommand(Vector2.UnitX, false, true));
        for (int i = 1; i < 60; i++) session.Step(Right);

        var old = session.Snapshot;
        var expected = (old.SessionId, old.Tick, old.SimulationSeconds, old.Phase, old.Ball,
            Coins: old.CollectedCoinIds.ToArray(), Poses: old.Hazards.ToArray());
        Assert.AreEqual(60L, expected.Tick);
        CollectionAssert.AreEqual(new[] { "coin" }, expected.Coins);
        Assert.IsGreaterThan(0f, expected.Ball.BoostRemainingSeconds);
        Assert.AreNotEqual(HazardMotion.Evaluate(spinner, 0d).Bounds.Orientation, expected.Poses[0].Bounds.Orientation);

        session.Restart();
        var fresh = session.Snapshot;
        Assert.AreNotEqual(expected.SessionId, fresh.SessionId);
        Assert.AreNotEqual(Guid.Empty, fresh.SessionId);
        Assert.AreEqual(0L, fresh.Tick);
        Assert.AreEqual(0d, fresh.SimulationSeconds);
        Assert.AreEqual(SessionPhase.Running, fresh.Phase);
        Assert.IsEmpty(fresh.CollectedCoinIds);
        Assert.AreEqual(0f, fresh.Ball.BoostRemainingSeconds);
        Assert.AreEqual(0f, fresh.Ball.BoostCooldownSeconds);
        Assert.AreEqual(new Vector3(0f, 0.501f, 0f), fresh.Ball.Center);
        var initialPose = HazardMotion.Evaluate(spinner, 0d).Bounds;
        Assert.AreEqual(initialPose.Center, fresh.Hazards.Single().Bounds.Center);
        Assert.AreEqual(initialPose.Orientation, fresh.Hazards.Single().Bounds.Orientation);
        Assert.IsEmpty(session.DrainEvents(), "непрочитанные события прежнего сеанса отброшены");
        Assert.IsEmpty(session.LastPath);

        // Прежний снимок — значение: ни Restart, ни следующие Step его не трогают.
        void AssertOldUnchanged(string stage)
        {
            Assert.AreEqual((expected.SessionId, expected.Tick, expected.SimulationSeconds, expected.Phase, expected.Ball),
                (old.SessionId, old.Tick, old.SimulationSeconds, old.Phase, old.Ball), stage);
            CollectionAssert.AreEqual(expected.Coins, old.CollectedCoinIds.ToArray(), stage);
            CollectionAssert.AreEqual(expected.Poses, old.Hazards.ToArray(), stage);
        }
        AssertOldUnchanged("после Restart");

        // Монета снова доступна и засчитывается уже в новом сеансе.
        var again = RunRight(session, 60);
        var collected = again.Single(e => e.Kind == GameEventKind.CoinCollected);
        Assert.AreEqual(fresh.SessionId, collected.SessionId);
        AssertOldUnchanged("после Step нового сеанса");
    }

    [TestMethod]
    public void SequenceStartsAtOneInEverySessionAndEventKeysNeverRepeat()
    {
        var coins = new[] { 1f, 2f, 3f }.Select(x => new CoinDefinition($"c{x}", new Vector3(x, 0.5f, 0f), 0.2f)).ToArray();
        var session = new GameSession(Arena(coins: coins), Defaults);
        var all = new List<GameEvent>();
        var ids = new List<Guid>();
        for (int round = 0; round < 3; round++)
        {
            ids.Add(session.Snapshot.SessionId);
            // Прыжок на tick 130 приземляется через 2·JumpSpeed/Gravity ≈ 0.67 с (80 tick),
            // поэтому к tick 230 шар снова на опоре.
            for (int tick = 0; tick < 230; tick++)
            {
                session.Step(new GameCommand(Vector2.UnitX, tick == 130, tick == 40));
                // Промежуточные выборки не должны сбивать нумерацию.
                if (tick % 7 == 0) all.AddRange(session.DrainEvents());
            }
            all.AddRange(session.DrainEvents());
            // Прыжок с опоры по контракту даёт Jumped; его не читают до Restart, и он пропадает.
            Assert.IsTrue(session.Snapshot.Ball.Grounded, "перед последним tick шар на опоре");
            session.Step(new GameCommand(Vector2.UnitX, true, false));
            session.Restart();
            Assert.IsEmpty(session.DrainEvents(), "непрочитанный Jumped прежнего сеанса отброшен");
        }

        Assert.HasCount(3, ids.Distinct().ToArray());
        foreach (var id in ids)
        {
            var own = all.Where(e => e.SessionId == id).ToArray();
            CollectionAssert.AreEqual(Enumerable.Range(1, own.Length).Select(i => (long)i).ToArray(),
                own.Select(e => e.Sequence).ToArray());
            Assert.IsTrue(own.Any(e => e.Kind == GameEventKind.Jumped));
            Assert.IsTrue(own.Any(e => e.Kind == GameEventKind.BoostStarted));
            Assert.AreEqual(3, own.Count(e => e.Kind == GameEventKind.CoinCollected));
        }
        Assert.HasCount(all.Count, all.Select(e => (e.SessionId, e.Sequence)).Distinct().ToArray());
        Assert.IsTrue(all.All(e => ids.Contains(e.SessionId)));
    }

    [TestMethod]
    public void ContactBudgetFailureLeavesSnapshotPathAndQueuedEventsUnchanged()
    {
        // Бюджет одного контакта. Шар падает, прижимаясь командой к стене (зазор 0.1 он
        // проходит за ~0.2 с при air control, падение 1.5 занимает ~0.41 с): в tick посадки
        // он касается и стены, и пола — второй контакт превышает бюджет. По пути вниз
        // успевает собрать монету.
        var settings = new SimulationSettings(maxContactIterations: 1);
        var wall = new SurfaceDefinition("wall", Box(new Vector3(1.1f, 2f, 0f), new Vector3(0.5f, 2f, 5f)));
        var coin = new CoinDefinition("coin", new Vector3(0f, 1.2f, 0f), 0.1f);
        var level = new LevelDefinition("regression", new Vector3(0f, 2f, 0f), -3f, FarFinish,
            [Floor(50f), wall], [coin], []);
        var session = new GameSession(level, settings);
        InvalidOperationException? failure = null;
        SessionSnapshot before = session.Snapshot;
        IReadOnlyList<MotionSegment> path = session.LastPath;
        for (int tick = 0; tick < 300 && failure is null; tick++)
        {
            before = session.Snapshot;
            path = session.LastPath;
            try { session.Step(Right); }
            catch (InvalidOperationException exception) { failure = exception; }
        }

        Assert.IsNotNull(failure, "шар должен упереться в стену и исчерпать бюджет");
        StringAssert.Contains(failure.Message, "MaxContactIterations");
        // Контракт: ошибка бюджета называет ID поверхности; в tick посадки это стена или пол.
        Assert.IsTrue(failure.Message.Contains("'wall'") || failure.Message.Contains("'floor'"), failure.Message);
        Assert.AreSame(before, session.Snapshot);
        Assert.AreSame(path, session.LastPath);
        Assert.AreEqual(SessionPhase.Running, before.Phase);
        CollectionAssert.AreEqual(new[] { "coin" }, before.CollectedCoinIds.ToArray());

        // Повтор того же шага падает так же: ошибка не оставила частичного прогресса.
        Assert.ThrowsExactly<InvalidOperationException>(() => session.Step(Right));
        Assert.AreSame(before, session.Snapshot);
        var queued = session.DrainEvents();
        Assert.AreEqual("coin", queued.Single().EntityId);
        Assert.AreEqual(1L, queued.Single().Sequence);
    }

    [TestMethod]
    public void PermutedLevelCollectionsProduceIdenticalEventsAndSnapshots()
    {
        // Реальные данные level2 + ещё две опасности: при трёх элементах все три
        // перестановки hazards различны (при двух они совпали бы в один обмен).
        var maze = TurningMaze.Create();
        HazardDefinition AuxBeam(string id, float z) => new(id, new Vector3(0f, 0.55f, z), Vector3.UnitY,
            Vector3.Zero, new Vector3(1f, 0.1f, 0.1f), Quaternion.Identity, -2f, 0.3f);
        HazardDefinition[] hazards = [.. maze.Hazards, AuxBeam("aux-beam", -30f), AuxBeam("aux-beam-2", -40f)];
        LevelDefinition Variant(int order) =>
            new(maze.Id, maze.Spawn, maze.KillY, maze.Finish,
                Reorder(maze.Surfaces, order), Reorder(maze.Coins, order), Reorder(hazards, order));

        var reference = Trace(Variant(0));
        Assert.IsTrue(reference.Events.Any(e => e.Kind == GameEventKind.CoinCollected));
        for (int order = 1; order <= 3; order++)
        {
            Assert.IsFalse(hazards.SequenceEqual(Reorder(hazards, order)), $"перестановка {order}");
            var actual = Trace(Variant(order));
            CollectionAssert.AreEqual(reference.Events, actual.Events);
            CollectionAssert.AreEqual(reference.Frames, actual.Frames);
        }
    }

    /// <summary>0 — исходный порядок; 1 — обратный; 2 — сдвиг на один; 3 — нечётные, затем чётные.</summary>
    private static T[] Reorder<T>(IReadOnlyList<T> items, int order) => order switch
    {
        0 => [.. items],
        1 => [.. Enumerable.Reverse(items)],
        2 => [.. items.Skip(1).Concat(items.Take(1))],
        _ => [.. items.Where((_, i) => i % 2 == 1).Concat(items.Where((_, i) => i % 2 == 0))],
    };

    private static (List<(long Sequence, long Tick, GameEventKind Kind, string? Id)> Events, List<string> Frames) Trace(LevelDefinition level)
    {
        var session = new GameSession(level, Defaults);
        var events = new List<(long Sequence, long Tick, GameEventKind Kind, string? Id)>();
        var frames = new List<string>();
        for (int tick = 0; tick < 400 && session.Snapshot.Phase == SessionPhase.Running; tick++)
        {
            session.Step(new GameCommand(Vector2.UnitX, tick == 30, tick == 60));
            events.AddRange(session.DrainEvents().Select(e => (e.Sequence, e.Tick, e.Kind, e.EntityId)));
            var s = session.Snapshot;
            frames.Add($"{s.Tick}|{s.Phase}|{s.Ball}|{string.Join(',', s.CollectedCoinIds)}|"
                + string.Join(';', s.Hazards.Select(h => $"{h.Id}:{h.Bounds.Center}:{h.Bounds.Orientation}")));
        }
        return (events, frames);
    }
}
