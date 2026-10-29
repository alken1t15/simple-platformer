using Engine3D.OpenGL;
using Platformer.App;

try
{
    if (args is ["--help"])
    {
        Console.WriteLine("Simple Platformer: integration preview, not a playable game.\nUsage: Platformer.App [--help | --smoke]\n--smoke: render 3 frames and close automatically.");
        return;
    }
    bool smoke = args is ["--smoke"];
    if (args.Length > 0 && !smoke)
        throw new ArgumentException("Unknown arguments. Use --help.");

    var snapshot = PreviewScene.Create();
    using var engine = Engine.Create(new EngineOptions
    {
        Title = "Simple Platformer — integration preview",
        Width = 960,
        Height = 640,
        FocusOnShow = !smoke,
    });
    engine.Run(snapshot.Scene, snapshot.Camera, _ =>
    {
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
