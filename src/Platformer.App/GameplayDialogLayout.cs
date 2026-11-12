using System.Numerics;
using Engine3D.Core;
using Engine3D.OpenGL;

namespace Platformer.App;

/// <summary>Компактные игровые диалоги; отрисовка и hit-test используют одни bounds.</summary>
internal static class GameplayDialogLayout
{
    internal static MenuView Build(AppScreen screen, MenuPage page, int selectedIndex, Vector2? pointer,
        string? pressedId, float width, float height)
    {
        var summary = page.Status!;
        bool pause = screen == AppScreen.Paused;
        bool won = screen == AppScreen.LevelResult;
        float scale = MathF.Min(width / 1280, height / 720);
        float left = (width - 1280 * scale) / 2, top = (height - 720 * scale) / 2;
        ColorRgba ink = new(0.94f, 0.98f, 1), muted = new(0.72f, 0.85f, 0.87f), accent = new(0.15f, 0.65f, 1);
        var controls = new List<MenuControl>();
        var panels = new List<MenuPanel>();
        var texts = new List<MenuText>();
        var images = new List<MenuImage>();
        MenuRect Rect(float x, float y, float w, float h) => new(left + x * scale, top + y * scale, w * scale, h * scale);
        void Text(string text, float x, float y, float size, ColorRgba color) =>
            texts.Add(new(new(left + x * scale, top + y * scale), text, size * scale, color));
        void Stars(int rating, float x, float y, float size)
        {
            for (int i = 0; i < 3; i++)
                images.Add(new(Rect(x + i * (size + 8), y, size, size),
                    i < rating ? MenuArtwork.FilledStar : MenuArtwork.EmptyStar));
        }
        panels.Add(new(new(0, 0, width, height), new(0.015f, 0.025f, 0.045f, 0.65f), MenuPanelKind.Backdrop));
        panels.Add(new(pause ? Rect(336, 176, 608, 344) : Rect(320, 150, 640, 426),
            new(0.035f, 0.10f, 0.14f, 0.96f), MenuPanelKind.Sheet));
        images.Add(new(pause ? Rect(368, 204, 64, 64) : Rect(352, 180, 64, 64),
            pause ? DialogArtwork.Pause : won ? DialogArtwork.Finish : DialogArtwork.Retry));
        Text(page.Title, pause ? 452 : 436, pause ? 204 : 180, 32, ink);
        Text(summary.LevelName, pause ? 452 : 436, pause ? 246 : 224, 19, muted);
        Text(page.Details[0], pause ? 368 : 352, pause ? 284 : 262, 16, muted);
        if (!pause)
        {
            panels.Add(new(Rect(352, 300, won ? 280 : 576, 100), new(0.05f, 0.19f, 0.22f), MenuPanelKind.Flat));
            if (won)
            {
                panels.Add(new(Rect(648, 300, 280, 100), new(0.05f, 0.19f, 0.22f), MenuPanelKind.Flat));
                Text("THIS FINISH", 372, 312, 15, ink);
                Text("PERSONAL BEST", 668, 312, 15, ink);
                Stars(summary.FinishStars!.Value, 372, 340, 44);
                Stars(summary.BestStars, 668, 340, 44);
            }
            else
            {
                Text("PERSONAL BEST", 376, 320, 18, ink);
                Text("Your saved finish", 376, 350, 14, muted);
                Stars(summary.BestStars, 740, 326, 44);
            }
        }
        for (int i = 0; i < page.Options.Count; i++)
        {
            var option = page.Options[i];
            var bounds = pause ? i switch
            {
                0 => Rect(368, 326, 544, 48), 1 => Rect(368, 386, 264, 44),
                2 => Rect(648, 386, 264, 44), _ => Rect(368, 442, 544, 40),
            } : i switch
            {
                0 => Rect(352, 422, 576, 48), 1 => Rect(352, 482, 280, 44), _ => Rect(648, 482, 280, 44),
            };
            bool selected = selectedIndex == i;
            controls.Add(new(option.Id, option.Label, option.Kind, bounds, selected,
                pointer is { } position && bounds.Contains(position), pressedId == option.Id, Enabled: option.Enabled));
            panels.Add(new(bounds, selected ? new(0.12f, 0.28f, 0.34f) : new(0.06f, 0.16f, 0.21f), MenuPanelKind.Control));
            if (selected || i == 0)
                panels.Add(new(new(bounds.X, bounds.Y + 9 * scale, 3 * scale, bounds.Height - 18 * scale), accent, MenuPanelKind.Focus));
            float font = (i == 0 ? 23 : 20) * scale;
            var metrics = OverlayTextMetrics.MeasureInkBounds(option.Label, font);
            texts.Add(new(new(bounds.X + 20 * scale, bounds.Y + (bounds.Height - metrics.Height) / 2 - metrics.Y), option.Label, font, ink));
        }
        return new(screen, new(width, height), controls, panels, texts, images);
    }
}
