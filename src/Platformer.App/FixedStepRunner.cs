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
    private bool _clearRequested;

    public FixedStepRunner(SimulationSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        FixedStepSeconds = settings.FixedStepSeconds;
    }

    public int Advance(double frameSeconds, GameCommand latestHeld, Action<GameCommand> step, bool jumpHeld = false)
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
                // Core принимает прыжок только с опоры. Удержание повторяет запрос после приземления,
                // включая несколько physics ticks в одном render frame; Boost остаётся фронтом.
                step(jumpHeld ? _pending with { JumpPressed = true } : _pending);
                ticks++;
                if (_clearRequested)
                {
                    _pending = default;
                    AccumulatedSeconds = 0d;
                    _clearRequested = false;
                    return ticks;
                }
                _pending = _pending with { JumpPressed = false, BoostPressed = false };
                AccumulatedSeconds -= FixedStepSeconds;
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
        finally { _advancing = false; _clearRequested = false; }
    }

    /// <summary>Callback перехода LevelFlow: внутри Step остановит catch-up после текущего tick.</summary>
    public void RequestClear()
    {
        if (_advancing) _clearRequested = true;
        else Clear();
    }

    /// <summary>Сбрасывает очередь и fault; после ошибки сначала восстановить сеанс. Диагностика накопительная.</summary>
    public void Clear()
    {
        if (_advancing) throw new InvalidOperationException("Нельзя очищать runner внутри Step.");
        AccumulatedSeconds = 0d;
        _pending = default;
        IsFaulted = false;
        _clearRequested = false;
    }
}
