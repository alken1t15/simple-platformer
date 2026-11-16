using Engine3D.OpenGL;

namespace Platformer.App;

/// <summary>Копирует один публичный снимок движка, прочитанный host внутри onUpdate.</summary>
public static class NativeInputAdapter
{
    private static readonly (InputKey Key, AppButtons Button)[] Keys =
    [
        (InputKey.W, AppButtons.W), (InputKey.A, AppButtons.A),
        (InputKey.S, AppButtons.S), (InputKey.D, AppButtons.D),
        (InputKey.Space, AppButtons.Space), (InputKey.LeftShift, AppButtons.LeftShift),
        (InputKey.RightShift, AppButtons.RightShift), (InputKey.R, AppButtons.R),
        (InputKey.Escape, AppButtons.Escape), (InputKey.Enter, AppButtons.Enter),
        (InputKey.Up, AppButtons.Up), (InputKey.Down, AppButtons.Down),
        (InputKey.Left, AppButtons.Left), (InputKey.Right, AppButtons.Right),
        (InputKey.Tab, AppButtons.Tab),
    ];

    public static AppInputSnapshot Map(InputSnapshot input, ControlBindings? bindings = null)
    {
        if (!input.IsFocused) return default;
        AppButtons down = default, pressed = default, released = default;
        foreach (var (key, button) in Keys)
        {
            if (bindings is not null && button is AppButtons.W or AppButtons.A or AppButtons.S or AppButtons.D
                or AppButtons.Space or AppButtons.LeftShift or AppButtons.RightShift or AppButtons.R) continue;
            if (input.IsDown(key)) down |= button;
            if (input.WasPressed(key)) pressed |= button;
            if (input.WasReleased(key)) released |= button;
        }
        if (bindings is not null)
            foreach (var (action, button) in new[] { (GameAction.Forward,AppButtons.W), (GameAction.Left,AppButtons.A),
                (GameAction.Backward,AppButtons.S), (GameAction.Right,AppButtons.D), (GameAction.Jump,AppButtons.Space),
                (GameAction.Boost,AppButtons.LeftShift), (GameAction.Restart,AppButtons.R) })
            {
                var key = bindings.For(action);
                if (input.IsDown(key)) down |= button;
                if (input.WasPressed(key)) pressed |= button;
                if (input.WasReleased(key)) released |= button;
                if (action == GameAction.Boost && key == InputKey.LeftShift
                    && !Enum.GetValues<GameAction>().Any(a=>a!=GameAction.Boost&&bindings.For(a)==InputKey.RightShift))
                {
                    if(input.IsDown(InputKey.RightShift)) down |= AppButtons.RightShift;
                    if(input.WasPressed(InputKey.RightShift)) pressed |= AppButtons.RightShift;
                    if(input.WasReleased(InputKey.RightShift)) released |= AppButtons.RightShift;
                }
            }
        var pointer = input.Pointer;
        if (pointer.IsDown(MouseButton.Left)) down |= AppButtons.PointerLeft;
        if (pointer.WasPressed(MouseButton.Left)) pressed |= AppButtons.PointerLeft;
        if (pointer.WasReleased(MouseButton.Left)) released |= AppButtons.PointerLeft;
        return new(input.IsFocused, down, pressed, pointer.Delta, pointer.Position, released);
    }
}
