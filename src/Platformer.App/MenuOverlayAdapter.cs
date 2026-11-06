using Engine3D.Core;

namespace Platformer.App;

/// <summary>Переносит готовый UI-кадр в публичные immutable DTO движка, в logical client pixels.</summary>
public static class MenuOverlayAdapter
{
    public static OverlayFrame Build(MenuView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        return new OverlayFrame(
            view.Panels.Select(panel => new OverlayPanel(new UiRect(panel.Bounds.X, panel.Bounds.Y,
                panel.Bounds.Width, panel.Bounds.Height), panel.Color)).ToArray(),
            [],
            view.Texts.Select(text => new OverlayText(text.Position, text.Text, text.PixelHeight, text.Color)).ToArray());
    }
}
