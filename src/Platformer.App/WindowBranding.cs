using Engine3D.OpenGL;

namespace Platformer.App;

/// <summary>Значок существующего окна через публичный API; Wayland/macOS используют launcher/bundle.</summary>
public static class WindowBranding
{
    public static string IconPath => Path.Combine(AppContext.BaseDirectory, "Assets", "Branding", "ball-adventure-window.png");
    public static bool Apply(Engine engine)
    {
        ArgumentNullException.ThrowIfNull(engine);
        return engine.SetWindowIcon(engine.LoadTexture(IconPath));
    }
}
