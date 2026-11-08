using Engine3D.Core;

namespace Platformer.App;

/// <summary>Переносит готовый UI-кадр в публичные immutable DTO движка, в logical client pixels.</summary>
public static class MenuOverlayAdapter
{
    public static OverlayFrame Build(MenuView view, bool visual = false)
    {
        ArgumentNullException.ThrowIfNull(view);
        return new OverlayFrame(
            view.Panels.Select(panel => new OverlayPanel(new UiRect(panel.Bounds.X, panel.Bounds.Y,
                panel.Bounds.Width, panel.Bounds.Height), visual ? TropicalColor(panel.Color) : panel.Color)).ToArray(),
            view.Controls.Where(control => control.PreviewBounds is not null).Select(control =>
            {
                var bounds = control.PreviewBounds!.Value;
                var skin = GameSkins.Get(control.Id["skin:".Length..]);
                return new OverlayImage(new UiRect(bounds.X, bounds.Y, bounds.Width, bounds.Height),
                    visual ? VisualScene.BallPreview(skin) : skin.Preview, control.Locked ? new(0.4f, 0.4f, 0.4f) : new(1, 1, 1));
            }).Concat(view.Images.Select(image => new OverlayImage(new UiRect(image.Bounds.X, image.Bounds.Y,
                image.Bounds.Width, image.Bounds.Height), image.Texture, new(1, 1, 1)))).ToArray(),
            view.Texts.Select(text => new OverlayText(text.Position, text.Text, text.PixelHeight, text.Color)).ToArray());
    }

    private static ColorRgba TropicalColor(ColorRgba color)
    {
        if (color == GameSkins.All[0].Material.BaseColor) return new(0.23f, 0.64f, 1f, color.A);
        if (color == GameSkins.All[1].Material.BaseColor) return new(0.98f, 0.54f, 0.2f, color.A);
        if (color.A < 0.7f) return new(0.04f, 0.16f, 0.22f, 0.28f);
        if (color.B > 0.8f && color.R < 0.3f) return new(1f, 0.78f, 0.25f, color.A);
        if (color.R < 0.2f && color.G < 0.4f && color.B < 0.6f)
            return new(0.035f + color.R * 0.35f, 0.17f + color.G * 0.45f, 0.22f + color.B * 0.32f,
                color.A < 1 ? 0.9f : 1);
        return color;
    }
}
