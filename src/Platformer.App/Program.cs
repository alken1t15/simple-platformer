using Engine3D.OpenGL;
using Engine3D.Audio;
using Platformer.App;
using Platformer.Core;

try
{
    if (args is ["--help"])
    {
        Console.WriteLine("""
            Ball Adventure: five levels, nine ball skins, menus, HUD and game audio.
            Usage: Platformer.App [--play | --legacy | --presenter [--skin ID] [--overview] [--still]] [--visual] [--smoke]
            Skins: classic-blue, orange, emerald, violet, basketball, football, neon, aurora, champion.
            Default / --play: open Main Menu. Arrows/Tab: choose; Enter/Space or mouse click: select.
            WASD: move; Space: jump (hold to jump again on landing); Shift: boost; R: restart; mouse: camera (up looks up).
            Escape: pause / back; Exit in Main Menu closes. Alt-Tab pauses; Continue resumes explicitly.
            --smoke: render 3 frames and close; no input, gameplay ticks, audio device or mouse capture.
            --trace-input: with gameplay mode, emit JSON frame/UI diagnostics for the X11 driver.
            --legacy: show the old integration preview; Escape closes.
            --presenter: show scripted 3D objects; Space switches skin, R resets, Escape closes.
            --visual: local tropical art experiment; works in gameplay and presenter modes.
            """);
        return;
    }
    bool smoke = false, playMode = false, legacyMode = false, traceInput = false, presenterMode = false, overview = false, still = false, visual = false;
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
            case "--visual": visual = true; break;
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
    if (visual && legacyMode) throw new ArgumentException("--visual is available in gameplay or presenter mode.");

    using var audio = gameplayMode && !smoke ? AudioDevice.Create(new AudioOptions()) : null;
    if (audio is { IsSilent: true }) Console.Error.WriteLine($"Audio: silent mode. {audio.Diagnostic}");
    using var gameAudio = audio is null ? null : new AudioCoordinator(
        new NativeAudioPlayback(audio, Path.Combine(AppContext.BaseDirectory, "Assets", "Audio")));
    var catalog = GameLevels.CreateCatalog();
    var progressStore = gameplayMode && !smoke ? new ProgressStore(ProgressStore.DefaultPath) : null;
    var progress = progressStore?.Load(catalog) ?? new PlayerProgress(catalog);
    if (progressStore?.Diagnostic is { } loadDiagnostic) Console.Error.WriteLine(loadDiagnostic);
    var settingsStore = gameplayMode && !smoke ? new SettingsStore(SettingsStore.DefaultPath) : null;
    var savedSettings = settingsStore?.Load(progress) ?? StoredSettings.Default;
    if (settingsStore?.Diagnostic is { } settingsDiagnostic) Console.Error.WriteLine(settingsDiagnostic);
    var persistence = settingsStore is null ? null : new SettingsPersistence(settingsStore, savedSettings, Console.Error.WriteLine);
    var game = gameplayMode ? new GameplayHost(catalog,
        SimulationSettings.Default, savedSettings.CreateUserSettings(), smoke,
        applyMasterVolume: audio is null ? null : value => audio.MasterVolume = value, audio: gameAudio, visual: visual, progress: progress) : null;
    if (game is not null && progressStore is not null)
        game.Flow.ProgressChanged += () =>
        {
            if (!progressStore.Save(progress)) Console.Error.WriteLine(progressStore.Diagnostic);
        };
    var preview = presenterMode ? new PresenterPreview(skin, overview, visual) : null;
    var snapshot = preview is null && game is null ? PreviewScene.Create() : null;
    using var engine = Engine.Create(new EngineOptions
    {
        Title = gameplayMode ? "Ball Adventure" : presenterMode
            ? "Ball Adventure — presenter preview" : "Simple Platformer — integration preview",
        Width = savedSettings.WindowedResolution.Width,
        Height = savedSettings.WindowedResolution.Height,
        Resizable = false,
        MinimumClientSize = (800, 450),
        LimitToMonitorWorkArea = true,
        FocusOnShow = !smoke,
    });
    WindowBranding.Apply(engine);
    if (game is not null && settingsStore is not null)
    {
        var initialClient = engine.ClientSize;
        var initialLimits = engine.WindowedClientSizeLimits;
        var desktop = engine.FullscreenSize;
        game.Menu.Window.Synchronize(initialClient.Width, initialClient.Height, initialLimits.MinimumWidth,
            initialLimits.MinimumHeight, initialLimits.MaximumWidth, initialLimits.MaximumHeight,
            false, desktop.Width, desktop.Height);
        game.Menu.Window.SelectActual();
    }
    bool restoreFullscreen = settingsStore is not null && savedSettings.Fullscreen;
    bool ready = false;
    try
    {
        engine.Run(game?.Scene ?? preview?.Presenter.Scene ?? snapshot!.Scene,
            game?.Camera ?? preview?.Camera ?? snapshot!.Camera, frameSeconds =>
        {
            // Единственное чтение snapshot за кадр; ticks не обращаются к окну.
            var input = engine.Input;
            if (game is not null)
            {
                // Сначала окно получает фокус: X11/GLFW не должны сворачивать ещё не активное fullscreen-окно.
                if (restoreFullscreen && input.IsFocused)
                {
                    engine.SetFullscreen(true);
                    restoreFullscreen = false;
                }
                var appInput = NativeInputAdapter.Map(input);
                var client = engine.ClientSize;
                var limits = engine.WindowedClientSizeLimits;
                var fullscreenSize = engine.FullscreenSize;
                game.Menu.Window.Synchronize(client.Width, client.Height, limits.MinimumWidth, limits.MinimumHeight,
                    limits.MaximumWidth, limits.MaximumHeight, engine.IsFullscreen, fullscreenSize.Width, fullscreenSize.Height);
                var frameResult = !smoke ? game.Advance(appInput, frameSeconds, client.Width, client.Height) : default;
                if (!restoreFullscreen) persistence?.Observe(game.Flow.UserSettings, game.Menu.Window, frameSeconds);
                if (game.Menu.Window.TakeDisplayRequest() is { } display)
                {
                    engine.SetFullscreen(display.Fullscreen);
                    if (!display.Fullscreen) engine.SetClientSize(display.Resolution.Width, display.Resolution.Height);
                }
                if (smoke) game.RefreshView(client.Width, client.Height);
                engine.SetOverlay(MenuOverlayAdapter.Build(game.View, visual));
                var cursorMode = game.CaptureRequested ? CursorMode.Captured : CursorMode.Normal;
                if (engine.CursorMode != cursorMode) engine.CursorMode = cursorMode;
                if (game.CloseRequested) engine.RequestClose();
                if (traceInput)
                {
                    var ball = game.Flow.Snapshot?.Ball;
                    var ballRotation = game.Flow.Presenter?.BallObject.Transform.Rotation;
                    Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new
                    {
                        kind = "frame", frame = engine.RenderedFrames, focused = input.IsFocused,
                        screen = game.Flow.Screen.ToString(), cursor = engine.CursorMode.ToString(),
                        down = appInput.Down.ToString(), pressed = appInput.Pressed.ToString(), released = appInput.Released.ToString(),
                        tick = game.Flow.Snapshot?.Tick, session = game.Flow.Snapshot?.SessionId,
                        level = game.Flow.Snapshot?.LevelId,
                        x = ball?.Center.X, y = ball?.Center.Y, z = ball?.Center.Z,
                        grounded = ball?.Grounded, velocityX = ball?.Velocity.X, velocityY = ball?.Velocity.Y,
                        ballRotationX = ballRotation?.X, ballRotationY = ballRotation?.Y,
                        ballRotationZ = ballRotation?.Z, ballRotationW = ballRotation?.W,
                        boost = ball?.BoostRemainingSeconds, cooldown = ball?.BoostCooldownSeconds,
                        ticks = frameResult.Ticks, moveX = frameResult.Input.Gameplay.MoveWorldXZ.X,
                        moveZ = frameResult.Input.Gameplay.MoveWorldXZ.Y,
                        jump = frameResult.Input.Gameplay.JumpPressed, jumpHeld = frameResult.Input.JumpHeld,
                        boostPressed = frameResult.Input.Gameplay.BoostPressed,
                        yaw = game.Frames.Camera.YawRadians, pitch = game.Frames.Camera.PitchRadians,
                        pointerX = appInput.PointerPosition.X, pointerY = appInput.PointerPosition.Y,
                        width = engine.FramebufferSize.Width, height = engine.FramebufferSize.Height,
                        clientWidth = client.Width, clientHeight = client.Height,
                        fullscreen = engine.IsFullscreen, fullscreenWidth = fullscreenSize.Width, fullscreenHeight = fullscreenSize.Height,
                        controls = game.View.Controls.Select(control => new
                        {
                            id = control.Id, label = control.Label, selected = control.Selected, enabled = control.Enabled, locked = control.Locked,
                            x = control.Bounds.X, y = control.Bounds.Y, width = control.Bounds.Width, height = control.Bounds.Height,
                        }),
                        uiTexts = game.View.Texts.Select(text => text.Text),
                        volume = game.Flow.UserSettings.MasterVolume, muted = game.Flow.UserSettings.Muted,
                        music = game.Flow.UserSettings.MusicTrackId,
                        skin = game.Flow.UserSettings.SkinId, coins = game.Flow.Snapshot?.CoinCount,
                        baseStars = game.Flow.Progress.BaseStars, adventureStars = game.Flow.Progress.TotalStars,
                        sceneCoins = game.Scene.Objects.Count(item => ReferenceEquals(item.Mesh, visual ? VisualScene.Coin : GameMeshes.Coin)),
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
    }
    finally { persistence?.Flush(); }
    if (smoke || traceInput || !gameplayMode)
        Console.WriteLine($"Rendered frames: {engine.RenderedFrames}");
}
catch (Exception exception)
{
    Console.Error.WriteLine($"Platformer: {exception.Message}");
    Environment.ExitCode = 1;
}
