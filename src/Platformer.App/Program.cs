using Engine3D.OpenGL;
using Engine3D.Audio;
using Platformer.App;
using Platformer.Core;

try
{
    if (args is ["--help"])
    {
        Console.WriteLine("""
            Ball Adventure: Training Track and Turning Maze, menus, HUD and game audio.
            Usage: Platformer.App [--play | --legacy | --presenter [--skin classic-blue|orange] [--overview] [--still]] [--smoke]
            Default / --play: open Main Menu. Arrows/Tab: choose; Enter/Space or mouse click: select.
            WASD: move; Space: jump; Shift: boost; R: restart; mouse: camera (up looks up).
            Escape: pause / back; Exit in Main Menu closes. Alt-Tab pauses; Continue resumes explicitly.
            --smoke: render 3 frames and close; no input, gameplay ticks, audio device or mouse capture.
            --trace-input: with gameplay mode, emit JSON frame/UI diagnostics for the X11 driver.
            --legacy: show the old integration preview; Escape closes.
            --presenter: show scripted 3D objects; Space switches skin, R resets, Escape closes.
            """);
        return;
    }
    bool smoke = false, playMode = false, legacyMode = false, traceInput = false, presenterMode = false, overview = false, still = false;
    string skin = "classic-blue";
    for (int index = 0; index < args.Length; index++)
    {
        switch (args[index])
        {
            case "--smoke": smoke = true; break;
            case "--play": playMode = true; break;
            case "--legacy": legacyMode = true; break;
            case "--trace-input": traceInput = true; break;
            case "--presenter": presenterMode = true; break;
            case "--overview": overview = true; break;
            case "--still": still = true; break;
            case "--skin" when index + 1 < args.Length: skin = args[++index]; break;
            default: throw new ArgumentException("Unknown arguments. Use --help.");
        }
    }
    if (!presenterMode && (overview || still || skin != "classic-blue" || args.Contains("--skin")))
        throw new ArgumentException("--skin, --overview and --still require --presenter.");
    if ((playMode ? 1 : 0) + (legacyMode ? 1 : 0) + (presenterMode ? 1 : 0) > 1)
        throw new ArgumentException("--play, --legacy and --presenter cannot be combined.");
    bool gameplayMode = !legacyMode && !presenterMode;
    if (traceInput && !gameplayMode) throw new ArgumentException("--trace-input requires gameplay mode.");

    using var audio = gameplayMode && !smoke ? AudioDevice.Create(new AudioOptions()) : null;
    if (audio is { IsSilent: true }) Console.Error.WriteLine($"Audio: silent mode. {audio.Diagnostic}");
    using var gameAudio = audio is null ? null : new AudioCoordinator(
        new NativeAudioPlayback(audio, Path.Combine(AppContext.BaseDirectory, "Assets", "Audio")));
    var game = gameplayMode ? new GameplayHost(GameLevels.CreateCatalog(),
        SimulationSettings.Default, new UserSettings(), smoke,
        applyMasterVolume: audio is null ? null : value => audio.MasterVolume = value, audio: gameAudio) : null;
    var preview = presenterMode ? new PresenterPreview(skin, overview) : null;
    var snapshot = preview is null && game is null ? PreviewScene.Create() : null;
    using var engine = Engine.Create(new EngineOptions
    {
        Title = gameplayMode ? "Ball Adventure" : presenterMode
            ? "Ball Adventure — presenter preview" : "Simple Platformer — integration preview",
        Width = 960,
        Height = 640,
        Resizable = false,
        MinimumClientSize = (800, 450),
        LimitToMonitorWorkArea = true,
        FocusOnShow = !smoke,
    });
    bool ready = false;
    engine.Run(game?.Scene ?? preview?.Presenter.Scene ?? snapshot!.Scene,
        game?.Camera ?? preview?.Camera ?? snapshot!.Camera, frameSeconds =>
    {
        // Единственное чтение snapshot за кадр; ticks не обращаются к окну.
        var input = engine.Input;
        if (game is not null)
        {
            var appInput = NativeInputAdapter.Map(input);
            var client = engine.ClientSize;
            var limits = engine.ClientSizeLimits;
            game.Menu.Window.Synchronize(client.Width, client.Height, limits.MinimumWidth, limits.MinimumHeight,
                limits.MaximumWidth, limits.MaximumHeight);
            var frameResult = !smoke ? game.Advance(appInput, frameSeconds, client.Width, client.Height) : default;
            if (game.Menu.Window.TakeRequest() is { } resolution)
                engine.SetClientSize(resolution.Width, resolution.Height);
            if (smoke) game.RefreshView(client.Width, client.Height);
            engine.SetOverlay(MenuOverlayAdapter.Build(game.View));
            var cursorMode = game.CaptureRequested ? CursorMode.Captured : CursorMode.Normal;
            if (engine.CursorMode != cursorMode) engine.CursorMode = cursorMode;
            if (game.CloseRequested) engine.RequestClose();
            if (traceInput)
            {
                var ball = game.Flow.Snapshot?.Ball;
                Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new
                {
                    kind = "frame", frame = engine.RenderedFrames, focused = input.IsFocused,
                    screen = game.Flow.Screen.ToString(), cursor = engine.CursorMode.ToString(),
                    down = appInput.Down.ToString(), pressed = appInput.Pressed.ToString(), released = appInput.Released.ToString(),
                    tick = game.Flow.Snapshot?.Tick, session = game.Flow.Snapshot?.SessionId,
                    level = game.Flow.Snapshot?.LevelId,
                    x = ball?.Center.X, y = ball?.Center.Y, z = ball?.Center.Z,
                    boost = ball?.BoostRemainingSeconds, cooldown = ball?.BoostCooldownSeconds,
                    ticks = frameResult.Ticks, moveX = frameResult.Input.Gameplay.MoveWorldXZ.X,
                    moveZ = frameResult.Input.Gameplay.MoveWorldXZ.Y,
                    jump = frameResult.Input.Gameplay.JumpPressed, boostPressed = frameResult.Input.Gameplay.BoostPressed,
                    yaw = game.Frames.Camera.YawRadians, pitch = game.Frames.Camera.PitchRadians,
                    pointerX = appInput.PointerPosition.X, pointerY = appInput.PointerPosition.Y,
                    width = engine.FramebufferSize.Width, height = engine.FramebufferSize.Height,
                    clientWidth = client.Width, clientHeight = client.Height,
                    controls = game.View.Controls.Select(control => new
                    {
                        id = control.Id, label = control.Label, selected = control.Selected,
                        x = control.Bounds.X, y = control.Bounds.Y, width = control.Bounds.Width, height = control.Bounds.Height,
                    }),
                    uiTexts = game.View.Texts.Select(text => text.Text),
                    volume = game.Flow.UserSettings.MasterVolume, muted = game.Flow.UserSettings.Muted,
                    skin = game.Flow.UserSettings.SkinId, coins = game.Flow.Snapshot?.CoinCount,
                    audioVolume = audio?.MasterVolume, audioSilent = audio?.IsSilent,
                }));
            }
        }
        else if (!smoke && input.WasPressed(InputKey.Escape))
        {
            engine.RequestClose();
            return;
        }
        if (preview is not null)
        {
            if (!smoke && input.WasPressed(InputKey.Space)) preview.ToggleSkin();
            if (!smoke && input.WasPressed(InputKey.R)) preview.Restart();
            if (!still) preview.Advance();
            if (!ready && engine.RenderedFrames >= 3)
            {
                Console.WriteLine("Presenter preview ready");
                ready = true;
            }
        }
        if (smoke && engine.RenderedFrames >= 3)
            engine.RequestClose();
    });
    Console.WriteLine($"Rendered frames: {engine.RenderedFrames}");
}
catch (Exception exception)
{
    Console.Error.WriteLine($"Platformer: {exception.Message}");
    Environment.ExitCode = 1;
}
