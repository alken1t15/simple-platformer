using System.Numerics;
using Engine3D.Core;
using Engine3D.OpenGL;

namespace Platformer.App;

/// <summary>Короткое подтверждение выхода; независимое от рейтинга и состояния попытки.</summary>
internal static class LeaveConfirmationLayout
{
    internal static MenuView Build(AppScreen screen, MenuPage page, int selectedIndex, Vector2? pointer,
        string? pressedId, float width, float height)
    {
        float scale = MathF.Min(width / 1280, height / 720);
        float left = (width - 1280 * scale) / 2, top = (height - 720 * scale) / 2;
        ColorRgba ink = new(0.94f, 0.98f, 1), muted = new(0.72f, 0.85f, 0.87f), accent = new(0.15f, 0.65f, 1);
        var controls = new List<MenuControl>();
        var panels = new List<MenuPanel>();
        var texts = new List<MenuText>();
        MenuRect Rect(float x, float y, float w, float h) => new(left + x * scale, top + y * scale, w * scale, h * scale);
        void Text(string text, float x, float y, float size, ColorRgba color) =>
            texts.Add(new(new(left + x * scale, top + y * scale), text, size * scale, color));

        panels.Add(new(new(0, 0, width, height), new(0.015f, 0.025f, 0.045f, 0.65f), MenuPanelKind.Backdrop));
        panels.Add(new(Rect(360, 230, 560, 260), new(0.035f, 0.10f, 0.14f, 0.96f), MenuPanelKind.Sheet));
        var images = new[] { new MenuImage(Rect(392, 258, 56, 56), DialogArtwork.Pause) };
        Text(page.Title, 468, 265, 30, ink);
        Text(page.Details[0], 392, 334, 19, ink);
        Text(page.Details[1], 392, 363, 17, muted);

        for (int i = 0; i < page.Options.Count; i++)
        {
            var option = page.Options[i];
            var bounds = Rect(392 + i * 256, 412, 240, 46);
            bool selected = selectedIndex == i;
            controls.Add(new(option.Id, option.Label, option.Kind, bounds, selected,
                pointer is { } position && bounds.Contains(position), pressedId == option.Id,
                Enabled: option.Enabled));
            panels.Add(new(bounds, selected ? new(0.12f, 0.28f, 0.34f) : new(0.06f, 0.16f, 0.21f), MenuPanelKind.Control));
            if (selected)
                panels.Add(new(new(bounds.X, bounds.Y + 9 * scale, 3 * scale, bounds.Height - 18 * scale), accent, MenuPanelKind.Focus));
            float font = 20 * scale;
            var metrics = OverlayTextMetrics.MeasureInkBounds(option.Label, font);
            texts.Add(new(new(bounds.X + 20 * scale, bounds.Y + (bounds.Height - metrics.Height) / 2 - metrics.Y), option.Label, font, ink));
        }
        return new(screen, new(width, height), controls, panels, texts, images);
    }
}
