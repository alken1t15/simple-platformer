using System.Numerics;
using Platformer.Core;

namespace Platformer.App;

public readonly record struct GameplayFrameResult(InputFrame Input, CameraPose? Camera, int Ticks);

/// <summary>CPU-порядок кадра для onUpdate/LevelFlow; не реализует сеанс или UI.</summary>
public sealed class GameplayFrameRunner
{
    public InputRouter Router { get; } = new();
    public FixedStepRunner Clock { get; }
    public CameraRig Camera { get; } = new();
    public AppScreen Screen { get; private set; } = AppScreen.MainMenu;
    public bool CaptureRequested => !_smoke && _lastInput.IsFocused && Screen == AppScreen.Playing;
    private readonly bool _smoke;
    private AppInputSnapshot _lastInput;
    private bool _suppressMouse = true;

    public GameplayFrameRunner(SimulationSettings settings, bool smoke = false)
    {
        Clock = new FixedStepRunner(settings);
        _smoke = smoke;
    }

    /// <summary>Playing задаётся только явным Start/Continue. После restart/next вызвать даже для того же экрана.</summary>
    public void SetScreen(AppScreen screen, bool resetCamera = false)
    {
        if (!Enum.IsDefined(screen)) throw new ArgumentOutOfRangeException(nameof(screen));
        if (screen == AppScreen.Playing && !_lastInput.IsFocused)
            throw new InvalidOperationException("Start/Continue требует фокус окна.");
        Clock.Clear();
        Router.Clear(_lastInput);
        _suppressMouse = true;
        if (resetCamera) Camera.Reset();
        Screen = screen;
    }

    public GameplayFrameResult Advance(AppInputSnapshot input, double frameSeconds, Vector3 target,
        LevelDefinition level, Action<GameCommand> step, IReadOnlyList<CrumblingPlatformState>? platforms = null)
    {
        if (!double.IsFinite(frameSeconds) || frameSeconds < 0d)
            throw new ArgumentOutOfRangeException(nameof(frameSeconds));
        ArgumentNullException.ThrowIfNull(level);
        ArgumentNullException.ThrowIfNull(step);
        bool focusChanged = input.IsFocused != _lastInput.IsFocused;
        _lastInput = input;
        if (focusChanged)
        {
            Clock.Clear();
            Router.Clear(input);
            _suppressMouse = true;
        }
        if (!input.IsFocused)
        {
            if (Screen == AppScreen.Playing) SetScreen(AppScreen.Paused);
            return default;
        }
        // Переходы определяются до движения камеры и до любого Step.
        InputFrame frame = Router.Map(input, Screen, Camera.YawRadians, frameSeconds);
        if (frame.PausePressed || frame.RestartPressed)
        {
            SetScreen(frame.PausePressed ? AppScreen.Paused : Screen);
            return new GameplayFrameResult(frame, null, 0);
        }
        if (Screen != AppScreen.Playing) return new GameplayFrameResult(frame, null, 0);
        CameraPose pose = Camera.Update(_suppressMouse || _smoke ? Vector2.Zero : input.MouseDelta,
            target, level, (float)Math.Min(frameSeconds, float.MaxValue), platforms);
        _suppressMouse = false;
        // Повторное чистое преобразование использует yaw именно текущего кадра.
        frame = Router.Map(input, Screen, pose.YawRadians);
        try
        {
            int ticks = Clock.Advance(frameSeconds, frame.Gameplay, step, frame.JumpHeld);
            return new GameplayFrameResult(frame, pose, ticks);
        }
        catch
        {
            // Не продолжать частично проваленный сеанс и не оставлять запрос capture.
            Screen = AppScreen.Failed;
            _suppressMouse = true;
            throw;
        }
    }
}
