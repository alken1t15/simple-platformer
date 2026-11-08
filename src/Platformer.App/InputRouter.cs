using System.Numerics;
using Platformer.Core;

namespace Platformer.App;

public sealed class InputRouter
{
    public InputFrame Map(Engine3D.OpenGL.InputSnapshot input, AppScreen screen, float yawRadians, double frameSeconds = 0d) =>
        Map(NativeInputAdapter.Map(input), screen, yawRadians, frameSeconds);

    private bool _focused = true;
    private AppButtons _blocked;
    private readonly NavigationRepeat _horizontal = new();
    private readonly NavigationRepeat _vertical = new();

    private void ResetRepeat() { _horizontal.Clear(); _vertical.Clear(); }

    /// <summary>После перехода удержания требуют отпускания и нового нажатия.</summary>
    public void Clear(AppInputSnapshot input)
    {
        _blocked = input.Down | input.Pressed;
        _focused = input.IsFocused;
        ResetRepeat();
    }

    public InputFrame Map(AppInputSnapshot input, AppScreen screen, float yawRadians, double frameSeconds = 0d)
    {
        if (!float.IsFinite(yawRadians)) throw new ArgumentOutOfRangeException(nameof(yawRadians));
        if (!Enum.IsDefined(screen)) throw new ArgumentOutOfRangeException(nameof(screen));
        if (!double.IsFinite(frameSeconds) || frameSeconds < 0d) throw new ArgumentOutOfRangeException(nameof(frameSeconds));
        if (!input.IsFocused)
        {
            _focused = false;
            _blocked = AppButtons.None;
            ResetRepeat();
            return default;
        }
        if (!_focused) _blocked = input.Down | input.Pressed;
        _focused = true;
        _blocked &= ~input.Released;
        _blocked &= input.Down | input.Pressed;
        AppButtons down = input.Down & ~_blocked;
        AppButtons pressed = input.Pressed & ~_blocked;
        bool Press(AppButtons keys) => (pressed & keys) != 0;
        int Held(AppButtons key) => (down & key) != 0 ? 1 : 0;
        bool restart = screen is AppScreen.Playing or AppScreen.Paused or AppScreen.Failed && Press(AppButtons.R);
        if (screen == AppScreen.Playing)
        {
            ResetRepeat();
            int forward = Held(AppButtons.W) - Held(AppButtons.S);
            int right = Held(AppButtons.D) - Held(AppButtons.A);
            float sin = MathF.Sin(yawRadians), cos = MathF.Cos(yawRadians);
            Vector2 move = new(right * cos - forward * sin, -right * sin - forward * cos);
            if (move.LengthSquared() > 1f) move = Vector2.Normalize(move);
            return new InputFrame(new GameCommand(move, Press(AppButtons.Space),
                Press(AppButtons.LeftShift | AppButtons.RightShift)), restart, Press(AppButtons.Escape), default,
                (down & AppButtons.Space) != 0);
        }
        // Повтор — правило UI, а не повторные Pressed от ОС. Активация/Escape/R остаются фронтами.
        bool Active(AppButtons keys) => ((down | pressed) & keys) != 0;
        bool reverseTab = Active(AppButtons.LeftShift | AppButtons.RightShift);
        int horizontalDirection = (Active(AppButtons.Right) ? 1 : 0) - (Active(AppButtons.Left) ? 1 : 0);
        int verticalDirection = (Active(AppButtons.Down) || (!reverseTab && Active(AppButtons.Tab)) ? 1 : 0)
            - (Active(AppButtons.Up) || (reverseTab && Active(AppButtons.Tab)) ? 1 : 0);
        int horizontal = _horizontal.Step(horizontalDirection, Press(AppButtons.Left | AppButtons.Right), frameSeconds);
        int vertical = _vertical.Step(verticalDirection, Press(AppButtons.Up | AppButtons.Down | AppButtons.Tab), frameSeconds);
        return new InputFrame(default, restart, false, new MenuInput(horizontal, vertical,
            Press(AppButtons.Enter | AppButtons.Space), Press(AppButtons.Escape),
            Press(AppButtons.PointerLeft), input.PointerPosition));
    }

    private sealed class NavigationRepeat
    {
        private const double Delay = 0.35d, Interval = 0.08d;
        private int direction;
        private bool armed;
        private double remaining;

        internal void Clear() { direction = 0; armed = false; remaining = 0d; }

        internal int Step(int heldDirection, bool freshPress, double frameSeconds)
        {
            if (heldDirection == 0) { Clear(); return 0; }
            if (heldDirection != direction || freshPress)
            {
                direction = heldDirection;
                armed = freshPress; // Нельзя начать повтор от старого удержания или смены только Shift.
                remaining = Delay;
                return freshPress ? direction : 0;
            }
            if (!armed) return 0;
            remaining -= Math.Min(frameSeconds, 0.25d);
            if (remaining > 0d) return 0;
            // Один шаг за кадр, без пачки действий после зависания; сохраняем долю интервала.
            remaining = Interval + remaining % Interval;
            return direction;
        }
    }
}
