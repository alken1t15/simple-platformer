using System.Numerics;
using Engine3D.Core;
using Platformer.Core;

namespace Platformer.App;

/// <summary>
/// Связывает ввод, LevelFlow, runner и публичные Scene/Camera на потоке окна.
/// Клавиатурный стенд #6; полноценные меню/HUD принадлежат #19.
/// </summary>
public sealed class GameplayHost
{
    private readonly LevelCatalog _catalog;
    private readonly GameScenePresenter _preview;
    private bool _resetRequested;
    private bool _wasFocused;

    public LevelFlow Flow { get; }
    public GameplayFrameRunner Frames { get; }
    public Scene Scene { get; } = new();
    public Camera Camera { get; } = new() { NearPlane = 0.05f, FarPlane = 150f };
    public bool CloseRequested { get; private set; }
    public bool CaptureRequested => !CloseRequested && Flow.Screen == AppScreen.Playing && Frames.CaptureRequested;

    public GameplayHost(LevelCatalog catalog, SimulationSettings settings, UserSettings userSettings, bool smoke = false)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        _catalog = catalog;
        Frames = new GameplayFrameRunner(settings, smoke);
        Flow = new LevelFlow(catalog, settings, userSettings, resetTransientState: () =>
        {
            // LevelFlow вызывает callback и из Step: Clear здесь был бы рекурсивным.
            Frames.Clock.RequestClear();
            _resetRequested = true;
        });
        _preview = new GameScenePresenter(catalog.Levels[0]);
        ApplyCamera(Frames.Camera.Update(Vector2.Zero, Target, CurrentLevel, 0f));
        SynchronizeScene();
    }

    public GameplayFrameResult Advance(AppInputSnapshot input, double frameSeconds)
    {
        if (!double.IsFinite(frameSeconds) || frameSeconds < 0d)
            throw new ArgumentOutOfRangeException(nameof(frameSeconds));
        if (CloseRequested) return default;
        if (!input.IsFocused && _wasFocused) Flow.LoseFocus();
        _wasFocused = input.IsFocused;

        GameplayFrameResult result = Frames.Advance(input, frameSeconds, Target, CurrentLevel, Flow.Step);
        bool resetCamera = false;
        if (input.IsFocused)
        {
            if (result.Input.PausePressed) Flow.Pause();
            else if (result.Input.RestartPressed)
            {
                Flow.Restart();
                resetCamera = true;
            }
            else if (result.Input.Menu.ActivatePressed)
            {
                switch (Flow.Screen)
                {
                    case AppScreen.MainMenu: Flow.Start(_catalog.Levels[0].Id); resetCamera = true; break;
                    case AppScreen.Paused: Flow.Continue(); break;
                    case AppScreen.Failed: Flow.Restart(); resetCamera = true; break;
                    case AppScreen.LevelResult: Flow.Next(); resetCamera = true; break;
                    case AppScreen.GameComplete: Flow.MainMenu(); resetCamera = true; break;
                }
            }
            else if (result.Input.Menu.BackPressed)
            {
                if (Flow.Screen == AppScreen.MainMenu) CloseRequested = true;
                else { Flow.MainMenu(); resetCamera = true; }
            }
        }
        if (_resetRequested || Frames.Screen != Flow.Screen)
        {
            Frames.SetScreen(Flow.Screen, resetCamera);
            _resetRequested = false;
        }
        if (resetCamera) ApplyCamera(Frames.Camera.Update(Vector2.Zero, Target, CurrentLevel, 0f));
        else if (result.Camera is { } pose) ApplyCamera(pose);
        SynchronizeScene();
        return result;
    }

    private LevelDefinition CurrentLevel => Flow.Snapshot is { } snapshot
        ? _catalog.Get(snapshot.LevelId) : _catalog.Levels[0];
    private Vector3 Target => (Flow.Snapshot?.Ball.Center ?? _catalog.Levels[0].Spawn) + new Vector3(0f, 0.3f, 0f);

    private void ApplyCamera(CameraPose pose)
    {
        Camera.Position = pose.Position;
        Camera.Target = pose.Target;
    }

    private void SynchronizeScene()
    {
        // Engine.Run удерживает один Scene весь lifecycle. Presenter меняется при Start/Next,
        // его объекты и удаление собранных монет переносим через публичный Scene API.
        Scene source = (Flow.Presenter ?? _preview).Scene;
        if (Scene.Objects.SequenceEqual(source.Objects)) return;
        var desired = new HashSet<SceneObject>(source.Objects);
        foreach (var item in Scene.Objects.ToArray())
            if (!desired.Contains(item)) Scene.Remove(item);
        var current = new HashSet<SceneObject>(Scene.Objects);
        foreach (var item in source.Objects)
            if (!current.Contains(item)) Scene.Add(item);
    }
}
