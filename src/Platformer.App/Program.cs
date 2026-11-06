using Engine3D.OpenGL;
using Platformer.App;
using Platformer.Core;

try
{
    if (args is ["--help"])
    {
        Console.WriteLine("Ball Adventure: integration previews and input/camera test host.\nUsage: Platformer.App [--smoke] [--play | --presenter [--skin classic-blue|orange] [--overview] [--still]]\n--smoke: render 3 frames and close automatically; never capture mouse.\n--play: control Training Track (full menus/audio/second level pending).\n--trace-input: with --play, emit JSON frame diagnostics for the X11 test driver.\nPlay host: Enter/Space = start/continue/retry/next; WASD = move; Space = jump; Shift = boost; R = restart; mouse = camera.\nEscape = pause / return to start / close from start. Alt-Tab pauses; Enter continues explicitly.\n--presenter: show 3D objects with scripted snapshots (no gameplay).\nPresenter: Space = switch skin; R = reset; Escape = close.");
        return;
    }
    bool smoke = false, playMode = false, traceInput = false, presenterMode = false, overview = false, still = false;
    string skin = "classic-blue";
    for (int index = 0; index < args.Length; index++)
    {
        switch (args[index])
        {
            case "--smoke": smoke = true; break;
            case "--play": playMode = true; break;
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
    if (playMode && presenterMode) throw new ArgumentException("--play and --presenter cannot be combined.");
    if (traceInput && !playMode) throw new ArgumentException("--trace-input requires --play.");

    var game = playMode ? new GameplayHost(new LevelCatalog([TrainingTrack.Create()]),
        SimulationSettings.Default, new UserSettings(), smoke) : null;
    var preview = presenterMode ? new PresenterPreview(skin, overview) : null;
    var snapshot = preview is null && game is null ? PreviewScene.Create() : null;
    using var engine = Engine.Create(new EngineOptions
    {
        Title = playMode ? "Ball Adventure - input/camera test" : presenterMode
            ? "Ball Adventure — presenter preview" : "Simple Platformer — integration preview",
        Width = 960,
        Height = 640,
        FocusOnShow = !smoke,
    });
    bool ready = false;
    AppScreen? lastScreen = null;
    if (game is not null) Console.WriteLine("Стенд #6: Enter — старт; Escape — пауза/назад/выход. Полное меню, звук и вторая локация ещё не подключены.");
    engine.Run(game?.Scene ?? preview?.Presenter.Scene ?? snapshot!.Scene,
        game?.Camera ?? preview?.Camera ?? snapshot!.Camera, frameSeconds =>
    {
        // Единственное чтение snapshot за кадр; ticks не обращаются к окну.
        var input = engine.Input;
        if (game is not null)
        {
            var appInput = NativeInputAdapter.Map(input);
            var frameResult = !smoke ? game.Advance(appInput, frameSeconds) : default;
            var cursorMode = game.CaptureRequested ? CursorMode.Captured : CursorMode.Normal;
            if (engine.CursorMode != cursorMode) engine.CursorMode = cursorMode;
            if (game.Flow.Screen != lastScreen)
            {
                Console.WriteLine($"Экран: {game.Flow.Screen}; Enter — старт/продолжить/повтор/далее; R — перезапуск; Escape — пауза/назад/выход.");
                lastScreen = game.Flow.Screen;
            }
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
                    x = ball?.Center.X, y = ball?.Center.Y, z = ball?.Center.Z,
                    boost = ball?.BoostRemainingSeconds, cooldown = ball?.BoostCooldownSeconds,
                    ticks = frameResult.Ticks, moveX = frameResult.Input.Gameplay.MoveWorldXZ.X,
                    moveZ = frameResult.Input.Gameplay.MoveWorldXZ.Y,
                    jump = frameResult.Input.Gameplay.JumpPressed, boostPressed = frameResult.Input.Gameplay.BoostPressed,
                    yaw = game.Frames.Camera.YawRadians, pitch = game.Frames.Camera.PitchRadians,
                    pointerX = appInput.PointerPosition.X, pointerY = appInput.PointerPosition.Y,
                    width = engine.FramebufferSize.Width, height = engine.FramebufferSize.Height,
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
