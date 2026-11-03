using System.Numerics;
using Platformer.Core;

namespace Platformer.App;

public sealed class InputRouter
{
    private bool _focused = true;
    private AppButtons _blocked;

    /// <summary>После перехода удержания требуют отпускания и нового нажатия.</summary>
    public void Clear(AppInputSnapshot input)
    {
        _blocked = input.Down | input.Pressed;
        _focused = input.IsFocused;
    }

    public InputFrame Map(AppInputSnapshot input, AppScreen screen, float yawRadians)
    {
        if (!float.IsFinite(yawRadians)) throw new ArgumentOutOfRangeException(nameof(yawRadians));
        if (!Enum.IsDefined(screen)) throw new ArgumentOutOfRangeException(nameof(screen));
        if (!input.IsFocused)
        {
            _focused = false;
            _blocked = AppButtons.None;
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
            int forward = Held(AppButtons.W) - Held(AppButtons.S);
            int right = Held(AppButtons.D) - Held(AppButtons.A);
            float sin = MathF.Sin(yawRadians), cos = MathF.Cos(yawRadians);
            Vector2 move = new(right * cos - forward * sin, -right * sin - forward * cos);
            if (move.LengthSquared() > 1f) move = Vector2.Normalize(move);
            return new InputFrame(new GameCommand(move, Press(AppButtons.Space),
                Press(AppButtons.LeftShift | AppButtons.RightShift)), restart, Press(AppButtons.Escape), default);
        }
        int horizontal = (Press(AppButtons.Right) ? 1 : 0) - (Press(AppButtons.Left) ? 1 : 0);
        int vertical = (Press(AppButtons.Down | AppButtons.Tab) ? 1 : 0) - (Press(AppButtons.Up) ? 1 : 0);
        return new InputFrame(default, restart, false, new MenuInput(horizontal, vertical,
            Press(AppButtons.Enter | AppButtons.Space), Press(AppButtons.Escape),
            Press(AppButtons.PointerLeft), input.PointerPosition));
    }
}
