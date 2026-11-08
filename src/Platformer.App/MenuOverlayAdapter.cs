using Engine3D.Core;

namespace Platformer.App;

/// <summary>Переносит готовый UI-кадр в публичные immutable DTO движка, в logical client pixels.</summary>
public static class MenuOverlayAdapter
{
    public static OverlayFrame Build(MenuView view, bool visual = false)
    {
        ArgumentNullException.ThrowIfNull(view);
        var backgrounds = visual ? view.Panels.Select(panel => TropicalMenuTheme.Panel(panel,
            panel.Kind == MenuPanelKind.Control ? view.Controls.FirstOrDefault(control => control.Bounds == panel.Bounds) : null,
            view.Screen == AppScreen.MainMenu))
            : Enumerable.Empty<OverlayImage>();
        return new OverlayFrame(
            visual ? [] : view.Panels.Select(panel => new OverlayPanel(new UiRect(panel.Bounds.X, panel.Bounds.Y,
                panel.Bounds.Width, panel.Bounds.Height), panel.Color)).ToArray(),
            backgrounds.Concat(view.Controls.Where(control => control.PreviewBounds is not null).Select(control =>
            {
                var bounds = control.PreviewBounds!.Value;
                var skin = GameSkins.Get(control.Id["skin:".Length..]);
                return new OverlayImage(new UiRect(bounds.X, bounds.Y, bounds.Width, bounds.Height),
                    visual ? VisualScene.BallPreview(skin) : skin.Preview, control.Locked ? new(0.4f, 0.4f, 0.4f) : new(1, 1, 1));
            })).Concat(view.Images.Select(image => new OverlayImage(new UiRect(image.Bounds.X, image.Bounds.Y,
                image.Bounds.Width, image.Bounds.Height), image.Texture, new(1, 1, 1)))).ToArray(),
            view.Texts.Select(text => new OverlayText(text.Position, text.Text, text.PixelHeight,
                visual ? TropicalMenuTheme.TextColor(text) : text.Color)).ToArray());
    }

}
