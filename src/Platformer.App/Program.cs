using Engine3D.OpenGL;
using Platformer.App;

try
{
    if (args is ["--help"])
    {
        Console.WriteLine("Ball Adventure: integration previews, not a playable game.\nUsage: Platformer.App [--smoke] [--presenter [--skin classic-blue|orange] [--overview] [--still]]\n--smoke: render 3 frames and close automatically.\n--presenter: show 3D objects with scripted snapshots (no gameplay).\nSpace: switch skin; R: reset presenter preview; Escape: close.");
        return;
    }
    bool smoke = false, presenterMode = false, overview = false, still = false;
    string skin = "classic-blue";
    for (int index = 0; index < args.Length; index++)
    {
        switch (args[index])
        {
            case "--smoke": smoke = true; break;
            case "--presenter": presenterMode = true; break;
            case "--overview": overview = true; break;
            case "--still": still = true; break;
            case "--skin" when index + 1 < args.Length: skin = args[++index]; break;
            default: throw new ArgumentException("Unknown arguments. Use --help.");
        }
    }
    if (!presenterMode && (overview || still || skin != "classic-blue" || args.Contains("--skin")))
        throw new ArgumentException("--skin, --overview and --still require --presenter.");

    var preview = presenterMode ? new PresenterPreview(skin, overview) : null;
    var snapshot = preview is null ? PreviewScene.Create() : null;
    using var engine = Engine.Create(new EngineOptions
    {
        Title = presenterMode ? "Ball Adventure — presenter preview" : "Simple Platformer — integration preview",
        Width = 960,
        Height = 640,
        FocusOnShow = !smoke,
    });
    bool ready = false;
    engine.Run(preview?.Presenter.Scene ?? snapshot!.Scene, preview?.Camera ?? snapshot!.Camera, _ =>
    {
        if (!smoke && engine.Input.WasPressed(InputKey.Escape))
        {
            engine.RequestClose();
            return;
        }
        if (preview is not null)
        {
            if (!smoke && engine.Input.WasPressed(InputKey.Space)) preview.ToggleSkin();
            if (!smoke && engine.Input.WasPressed(InputKey.R)) preview.Restart();
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
