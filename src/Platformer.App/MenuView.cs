using System.Numerics;
using Engine3D.Core;

namespace Platformer.App;

/// <summary>Логические пиксели client area: начало слева сверху, Y вниз.</summary>
public readonly record struct MenuRect(float X, float Y, float Width, float Height)
{
    public bool Contains(Vector2 point) => point.X >= X && point.X < X + Width
        && point.Y >= Y && point.Y < Y + Height;
}

public readonly record struct MenuPanel(MenuRect Bounds, ColorRgba Color);
public readonly record struct MenuText(Vector2 Position, string Text, float PixelHeight, ColorRgba Color);
public readonly record struct MenuImage(MenuRect Bounds, TextureData Texture);
public enum MenuControlKind { Button, Level, Volume, Skin }
public sealed record MenuControl(string Id, string Label, MenuControlKind Kind, MenuRect Bounds,
    bool Selected, bool Hovered, bool Pressed, MenuRect? ValueBounds = null, MenuRect? PreviewBounds = null, bool Enabled = true, bool Locked = false);

/// <summary>
/// Отделённый неизменяемый кадр UI. Оконный adapter переводит Panels/Texts в публичный overlay.
/// Controls используют те же Bounds, что панели и подписи; GPU-ресурсов здесь нет.
/// </summary>
public sealed class MenuView
{
    public AppScreen Screen { get; }
    public Vector2 ClientSize { get; }
    public IReadOnlyList<MenuControl> Controls { get; }
    public IReadOnlyList<MenuPanel> Panels { get; }
    public IReadOnlyList<MenuText> Texts { get; }
    public IReadOnlyList<MenuImage> Images { get; }

    internal MenuView(AppScreen screen, Vector2 clientSize, IEnumerable<MenuControl> controls,
        IEnumerable<MenuPanel> panels, IEnumerable<MenuText> texts, IEnumerable<MenuImage>? images = null)
    {
        Screen = screen;
        ClientSize = clientSize;
        Controls = Array.AsReadOnly(controls.ToArray());
        Panels = Array.AsReadOnly(panels.ToArray());
        Texts = Array.AsReadOnly(texts.ToArray());
        Images = Array.AsReadOnly(images?.ToArray() ?? []);
    }
}
