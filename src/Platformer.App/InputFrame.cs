using System.Numerics;
using Platformer.Core;

namespace Platformer.App;

public readonly record struct InputFrame(GameCommand Gameplay, bool RestartPressed,
    bool PausePressed, MenuInput Menu);

public readonly record struct MenuInput(int HorizontalStep, int VerticalStep,
    bool ActivatePressed, bool BackPressed, bool PointerPressed, Vector2 PointerPosition);

/// <summary>Независимые от окна кнопки CPU-части App; native adapter явно сопоставляет InputKey.</summary>
[Flags]
public enum AppButtons
{
    None = 0, W = 1, A = 2, S = 4, D = 8, Space = 16, LeftShift = 32,
    RightShift = 64, R = 128, Escape = 256, Enter = 512, Up = 1024,
    Down = 2048, Left = 4096, Right = 8192, Tab = 16384, PointerLeft = 32768
}

/// <summary>Неизменяемый контрактный снимок; в native adapter заполняется одним Engine.Input в onUpdate.</summary>
public readonly record struct AppInputSnapshot(bool IsFocused, AppButtons Down = AppButtons.None,
    AppButtons Pressed = AppButtons.None, Vector2 MouseDelta = default, Vector2 PointerPosition = default,
    AppButtons Released = AppButtons.None);
