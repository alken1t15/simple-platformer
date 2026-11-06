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

    public static AppInputSnapshot Map(InputSnapshot input)
    {
        if (!input.IsFocused) return default;
        AppButtons down = default, pressed = default, released = default;
        foreach (var (key, button) in Keys)
        {
            if (input.IsDown(key)) down |= button;
            if (input.WasPressed(key)) pressed |= button;
            if (input.WasReleased(key)) released |= button;
        }
        var pointer = input.Pointer;
        if (pointer.IsDown(MouseButton.Left)) down |= AppButtons.PointerLeft;
        if (pointer.WasPressed(MouseButton.Left)) pressed |= AppButtons.PointerLeft;
        if (pointer.WasReleased(MouseButton.Left)) released |= AppButtons.PointerLeft;
        return new(input.IsFocused, down, pressed, pointer.Delta, pointer.Position, released);
    }
}
