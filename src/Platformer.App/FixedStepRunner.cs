using Platformer.Core;

namespace Platformer.App;

/// <summary>Один экземпляр на игровой поток; callback не должен рекурсивно вызывать Advance/Clear.</summary>
public sealed class FixedStepRunner
{
    public const double MaximumFrameSeconds = 0.1d;
    public const int MaximumTicksPerFrame = 8;
    public double FixedStepSeconds { get; }
    public double AccumulatedSeconds { get; private set; }
    public double DroppedSeconds { get; private set; }
    public bool IsFaulted { get; private set; }
    private GameCommand _pending;
    private bool _advancing;

    public FixedStepRunner(SimulationSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        FixedStepSeconds = settings.FixedStepSeconds;
    }

    public int Advance(double frameSeconds, GameCommand latestHeld, Action<GameCommand> step)
    {
        if (!double.IsFinite(frameSeconds) || frameSeconds < 0d)
            throw new ArgumentOutOfRangeException(nameof(frameSeconds));
        ArgumentNullException.ThrowIfNull(step);
        double length = (double)latestHeld.MoveWorldXZ.X * latestHeld.MoveWorldXZ.X
            + (double)latestHeld.MoveWorldXZ.Y * latestHeld.MoveWorldXZ.Y;
        if (!double.IsFinite(length) || length > 1.000002d)
            throw new ArgumentOutOfRangeException(nameof(latestHeld));
        if (_advancing || IsFaulted)
            throw new InvalidOperationException("Runner занят или требует Clear после ошибки сеанса.");

        _pending = latestHeld with
        {
            JumpPressed = _pending.JumpPressed || latestHeld.JumpPressed,
            BoostPressed = _pending.BoostPressed || latestHeld.BoostPressed
        };
        DroppedSeconds += Math.Max(0d, frameSeconds - MaximumFrameSeconds);
        AccumulatedSeconds += Math.Min(frameSeconds, MaximumFrameSeconds);
        _advancing = true;
        try
        {
            int ticks = 0;
            while (AccumulatedSeconds >= FixedStepSeconds && ticks < MaximumTicksPerFrame)
            {
                step(_pending);
                _pending = _pending with { JumpPressed = false, BoostPressed = false };
                AccumulatedSeconds -= FixedStepSeconds;
                ticks++;
            }
            double dropped = Math.Floor(AccumulatedSeconds / FixedStepSeconds) * FixedStepSeconds;
            DroppedSeconds += dropped;
            AccumulatedSeconds -= dropped;
            return ticks;
        }
        catch
        {
            IsFaulted = true;
            AccumulatedSeconds = 0d;
            _pending = default;
            throw;
        }
        finally { _advancing = false; }
    }

    /// <summary>Сбрасывает очередь и fault; после ошибки сначала восстановить сеанс. Диагностика накопительная.</summary>
    public void Clear()
    {
        if (_advancing) throw new InvalidOperationException("Нельзя очищать runner внутри Step.");
        AccumulatedSeconds = 0d;
        _pending = default;
        IsFaulted = false;
    }
}
