namespace Platformer.Core;

/// <summary>CPU-сеанс: один атомарный tick, одноразовые события и полный reset.</summary>
public sealed class GameSession
{
    private static readonly IReadOnlyList<MotionSegment> EmptyPath = Array.AsReadOnly(Array.Empty<MotionSegment>());
    private readonly LevelDefinition level;
    private readonly SimulationSettings settings;
    private readonly BallSimulator simulator;
    private readonly Func<BallState, GameCommand, BallStepResult> simulate;
    private readonly CoinDefinition[] coins;
    private readonly HazardDefinition[] hazards;
    private GameEvent[] events = [];
    private long sequence;

    public SessionSnapshot Snapshot { get; private set; }
    /// <summary>Полный read-only Path последнего успешного tick; пуст до первого Step и после Restart.</summary>
    public IReadOnlyList<MotionSegment> LastPath { get; private set; } = EmptyPath;

    public GameSession(LevelDefinition level, SimulationSettings settings) : this(level, settings, null) { }

    // Внутренний стык позволяет воспроизводить сбой физики/Path, не меняя публичный контракт.
    internal GameSession(LevelDefinition level, SimulationSettings settings,
        Func<BallState, GameCommand, BallStepResult>? simulate)
    {
        ArgumentNullException.ThrowIfNull(level);
        ArgumentNullException.ThrowIfNull(settings);
        this.level = level;
        this.settings = settings;
        simulator = new BallSimulator(level, settings);
        this.simulate = simulate ?? simulator.Step;
        coins = level.Coins.OrderBy(coin => coin.Id, StringComparer.Ordinal).ToArray();
        hazards = level.Hazards.OrderBy(hazard => hazard.Id, StringComparer.Ordinal).ToArray();
        ValidateHazardBudget();
        Snapshot = Initial();
    }

    public void Step(GameCommand command)
    {
        command.ValidatedMove();
        var before = Snapshot;
        if (before.Phase != SessionPhase.Running) return;
        long tick = AdvanceCounter(before.Tick, 1);
        double seconds = tick * (double)settings.FixedStepSeconds;
        if (!double.IsFinite(seconds) || seconds <= before.SimulationSeconds)
            throw new InvalidOperationException("Исчерпана точность времени сеанса");
        var result = simulate(before.Ball, command);
        var pending = new List<(GameEventKind Kind, string? Id)>();
        if (command.BoostPressed && before.Ball.BoostRemainingSeconds == 0f && before.Ball.BoostCooldownSeconds == 0f)
            pending.Add((GameEventKind.BoostStarted, null));
        if (command.JumpPressed && before.Ball.Grounded)
            pending.Add((GameEventKind.Jumped, null));
        var collected = new HashSet<string>(before.CollectedCoinIds, StringComparer.Ordinal);
        var phase = SessionPhase.Running;
        bool hazardHit = hazards.Any(hazard =>
            HazardCollision.IntersectsPath(hazard, result.Path, before.SimulationSeconds, settings));
        if ((double)result.Ball.Center.Y + result.Ball.Radius < level.KillY || hazardHit)
        {
            phase = SessionPhase.Failed;
            pending.Add((GameEventKind.Failed, null));
        }
        else
        {
            foreach (var coin in coins)
                if (!collected.Contains(coin.Id) && TriggerQueries.Coin(result.Path, coin, result.Ball.Radius))
                {
                    collected.Add(coin.Id);
                    pending.Add((GameEventKind.CoinCollected, coin.Id));
                }
            if (TriggerQueries.Box(result.Path, level.Finish, result.Ball.Radius))
            {
                phase = SessionPhase.LevelComplete;
                pending.Add((GameEventKind.LevelCompleted, null));
            }
        }
        long nextSequence = AdvanceCounter(sequence, pending.Count);
        var next = new SessionSnapshot(before.SessionId, level.Id, tick, seconds, phase,
            result.Ball, collected.ToArray(), HazardPoses(seconds));
        long queueLength = AdvanceCounter(events.Length, pending.Count);
        if (queueLength > int.MaxValue) throw new InvalidOperationException("Переполнение очереди событий");
        var queue = new GameEvent[(int)queueLength];
        events.CopyTo(queue, 0);
        for (int i = 0; i < pending.Count; i++)
            queue[events.Length + i] = new GameEvent(before.SessionId, sequence + i + 1, tick, pending[i].Kind, pending[i].Id);
        // Ни одна наблюдаемая часть состояния не меняется до успешной подготовки tick.
        Snapshot = next;
        LastPath = result.Path;
        sequence = nextSequence;
        events = queue;
    }

    public IReadOnlyList<GameEvent> DrainEvents()
    {
        var drained = Array.AsReadOnly(events);
        events = [];
        return drained;
    }

    public void Restart()
    {
        var initial = Initial();
        Snapshot = initial;
        LastPath = EmptyPath;
        sequence = 0;
        events = [];
    }

    private SessionSnapshot Initial() => new(Guid.NewGuid(), level.Id, 0, 0d, SessionPhase.Running,
        simulator.CreateInitialState(), [], HazardPoses(0d));

    private HazardPose[] HazardPoses(double seconds) =>
        hazards.Select(hazard => HazardMotion.Evaluate(hazard, seconds)).ToArray();

    private void ValidateHazardBudget()
    {
        double horizontal = (double)settings.MoveSpeed * settings.BoostMultiplier;
        double ballTravel = Math.Sqrt(horizontal * horizontal + (double)settings.MaxFallSpeed * settings.MaxFallSpeed)
            * settings.FixedStepSeconds;
        foreach (var hazard in hazards)
        {
            double reach = (double)hazard.CenterOffset.Length() + hazard.HalfExtents.Length();
            double beamTravel = Math.Abs((double)hazard.AngularSpeed) * settings.FixedStepSeconds * reach;
            if (Math.Ceiling((ballTravel + beamTravel) / (settings.BallRadius / 4d)) > settings.MaxCollisionSubsteps)
                throw new ArgumentException($"Бюджет подшагов не покрывает движение шара и опасности '{hazard.Id}'", nameof(level));
        }
    }

    internal static long AdvanceCounter(long value, int count)
    {
        try { return checked(value + count); }
        catch (OverflowException exception) { throw new InvalidOperationException("Переполнение счётчика сеанса", exception); }
    }
}
