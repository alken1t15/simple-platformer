using System.Numerics;
using Engine3D.Core;
using Platformer.Core;

namespace Platformer.App;

/// <summary>
/// Связывает ввод, LevelFlow, runner и публичные Scene/Camera на потоке окна.
/// MenuController владеет выбором и переходами; host — временем, камерой и сценой.
/// </summary>
public sealed class GameplayHost
{
    private readonly LevelCatalog _catalog;
    private readonly GameScenePresenter _preview;
    private readonly bool _visual;
    private GameScenePresenter? _menuBackdrop;
    private bool _resetRequested;
    private bool _resetMenuPointer = true;
    private readonly AudioCoordinator? _audio;

    public LevelFlow Flow { get; }
    public GameplayFrameRunner Frames { get; }
    public MenuController Menu { get; }
    public MenuView View { get; private set; }
    public Scene Scene { get; } = new();
    public Camera Camera { get; } = new() { NearPlane = 0.05f, FarPlane = 150f };
    public bool CloseRequested => Menu.CloseRequested;
    public bool CaptureRequested => !CloseRequested && Flow.Screen == AppScreen.Playing && Frames.CaptureRequested;

    public GameplayHost(LevelCatalog catalog, SimulationSettings settings, UserSettings userSettings, bool smoke = false,
        Action<float>? applyMasterVolume = null, AudioCoordinator? audio = null, bool visual = false, PlayerProgress? progress = null)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        _catalog = catalog;
        _visual = visual;
        _audio = smoke ? null : audio;
        Frames = new GameplayFrameRunner(settings, smoke);
        Flow = new LevelFlow(catalog, settings, userSettings, resetTransientState: () =>
        {
            // LevelFlow вызывает callback и из Step: Clear здесь был бы рекурсивным.
            Frames.Clock.RequestClear();
            _resetRequested = true;
        }, stopGameplayAudio: () => _audio?.Stop(), setAudioPaused: value => _audio?.SetPaused(value), visual: visual, progress: progress);
        Menu = new MenuController(Flow, catalog, applyMasterVolume);
        View = Menu.BuildView(960f, 640f);
        _preview = new GameScenePresenter(catalog.Levels[0], visual);
        if (visual)
        {
            VisualScene.ConfigureLighting(Scene);
            Camera.FarPlane = 220f;
        }
        ApplyCamera(Frames.Camera.Update(Vector2.Zero, Target, CurrentLevel, 0f));
        SynchronizeScene();
    }

    public GameplayFrameResult Advance(AppInputSnapshot input, double frameSeconds,
        float clientWidth = 960f, float clientHeight = 640f)
    {
        if (!double.IsFinite(frameSeconds) || frameSeconds < 0d)
            throw new ArgumentOutOfRangeException(nameof(frameSeconds));
        MenuLayout.ValidateSize(clientWidth, clientHeight);
        if (CloseRequested) return default;
        bool wasPlaying = Flow.Screen == AppScreen.Playing;
        if (_resetMenuPointer && !wasPlaying && input.IsFocused)
        {
            Menu.ResetPointerPosition(input.PointerPosition);
            _resetMenuPointer = false;
        }

        GameplayFrameResult result = Frames.Advance(input, frameSeconds, Target, CurrentLevel, Flow.Step, Flow.Snapshot?.Platforms);
        var menuResult = Menu.Handle(result.Input, clientWidth, clientHeight, input.IsFocused);
        bool resetCamera = menuResult.ResetCamera;
        if (wasPlaying && Flow.Screen != AppScreen.Playing) _resetMenuPointer = true;
        if (_resetRequested || menuResult.ResetInput || Frames.Screen != Flow.Screen)
        {
            Frames.SetScreen(Flow.Screen, resetCamera);
            _resetRequested = false;
        }
        if (resetCamera) ApplyCamera(Frames.Camera.Update(Vector2.Zero, Target, CurrentLevel, 0f));
        else if (result.Camera is { } pose) ApplyCamera(pose);
        SynchronizeScene();
        RefreshView(clientWidth, clientHeight);
        if (_audio is not null)
        {
            if (Flow.Snapshot is { } snapshot) _audio.Consume(snapshot.SessionId, Flow.DrainEvents());
            _audio.SetScreen(Flow.Screen);
        }
        return result;
    }

    /// <summary>Resize/smoke обновляют UI без игрового ввода и ticks.</summary>
    public void RefreshView(float clientWidth, float clientHeight) => View = Menu.BuildView(clientWidth, clientHeight);

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
        if (Flow.Presenter is { } presenter) _menuBackdrop = presenter;
        var visible = Flow.Presenter ?? _menuBackdrop ?? _preview;
        Scene source = visible.Scene;
        var skin = GameSkins.Get(Flow.UserSettings.SkinId);
        visible.BallObject.Material = _visual ? VisualScene.BallMaterial(skin) : skin.Material;
        if (Scene.Objects.SequenceEqual(source.Objects)) return;
        var desired = new HashSet<SceneObject>(source.Objects);
        foreach (var item in Scene.Objects.ToArray())
            if (!desired.Contains(item)) Scene.Remove(item);
        var current = new HashSet<SceneObject>(Scene.Objects);
        foreach (var item in source.Objects)
            if (!current.Contains(item)) Scene.Add(item);
    }
}
