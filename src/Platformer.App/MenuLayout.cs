using System.Numerics;
using Engine3D.Core;
using Engine3D.OpenGL;

namespace Platformer.App;

internal enum MenuAction { Play, Levels, Settings, Skins, Exit, ChooseLevel, Back, Volume, Mute,
    ChooseSkin, Continue, Restart, MainMenu, Next, Resolution, ApplyResolution, DisplayMode, CancelMainMenu, ConfirmMainMenu, Music, Controls }
internal sealed record MenuOption(string Id, string Label, MenuAction Action,
    MenuControlKind Kind = MenuControlKind.Button, string? Value = null, ColorRgba? Swatch = null, bool Enabled = true, string? Detail = null, bool Locked = false, int? Rating = null, LevelDifficulty? Difficulty = null);
internal sealed record MenuPage(string Title, IReadOnlyList<string> Details, IReadOnlyList<MenuOption> Options, bool IsControlsPage = false);

/// <summary>Один расчёт геометрии для отрисовки и hit-test; не обращается к окну.</summary>
internal static class MenuLayout
{
    private static readonly ColorRgba White = new(0.94f, 0.96f, 1f);
    private static readonly ColorRgba Muted = new(0.68f, 0.74f, 0.84f);
    private static readonly ColorRgba Accent = new(0.15f, 0.65f, 1f);

    internal static void ValidateSize(float width, float height)
    {
        if (!float.IsFinite(width) || width < 0f) throw new ArgumentOutOfRangeException(nameof(width));
        if (!float.IsFinite(height) || height < 0f) throw new ArgumentOutOfRangeException(nameof(height));
    }

    internal static MenuView Build(AppScreen screen, MenuPage page, HudModel? hud, float volume,
        int selectedIndex, Vector2? pointer, string? pressedId, float width, float height, bool visual = false)
    {
        ValidateSize(width, height);
        var controls = new List<MenuControl>();
        var panels = new List<MenuPanel>();
        var texts = new List<MenuText>();
        var images = new List<MenuImage>();
        if (width == 0f || height == 0f) return new(screen, new(width, height), controls, panels, texts);
        float scale = MathF.Min(width / 1280f, height / 720f);
        float left = (width - 1280f * scale) / 2f, top = (height - 720f * scale) / 2f;
        MenuRect Rect(float x, float y, float w, float h) => new(left + x * scale, top + y * scale, w * scale, h * scale);
        void Text(string text, float x, float y, float size, ColorRgba? color = null, MenuTextKind kind = MenuTextKind.Default) =>
            texts.Add(new(new(left + x * scale, top + y * scale), text, size * scale, color ?? White, kind));
        void Label(string text, float x, float y, float height, float size, ColorRgba? color = null)
        {
            var ink = OverlayTextMetrics.MeasureInkBounds(text, size);
            Text(text, x, y + (height - ink.Height) / 2 - ink.Y, size, color);
        }
        void CardText(string title, string detail, float x, float y, float height,
            float titleTop, float detailTop, float titleSize, float detailSize, ColorRgba ink, ColorRgba detailInk,
            MenuTextKind detailKind = MenuTextKind.Default)
        {
            var first = OverlayTextMetrics.MeasureInkBounds(title, titleSize);
            var second = OverlayTextMetrics.MeasureInkBounds(detail, detailSize);
            float topInk = MathF.Min(titleTop + first.Y, detailTop + second.Y);
            float bottomInk = MathF.Max(titleTop + first.Y + first.Height, detailTop + second.Y + second.Height);
            float offset = (height - (bottomInk - topInk)) / 2 - topInk;
            Text(title, x, y + titleTop + offset, titleSize, ink);
            Text(detail, x, y + detailTop + offset, detailSize, detailInk, detailKind);
        }

        if (visual && screen == AppScreen.MainMenu)
        {
            panels.Add(new(new(0, 0, width, height), new(1, 1, 1), MenuPanelKind.Backdrop));
            Text("BALL", 104, 88, 70, Accent);
            Text("ADVENTURE", 104, 163, 48);
            Text("ROLL. JUMP. EXPLORE.", 108, 220, 16, Muted);
            for (int i = 0; i < page.Details.Count; i++) Text(page.Details[i], 108, 255 + i * 24, 18, Muted);
            for (int i = 0; i < page.Options.Count; i++)
            {
                var option = page.Options[i];
                float y = 305 + i * 48;
                var bounds = Rect(104, y, 360, 40);
                bool selected = selectedIndex == i;
                bool hovered = pointer is { } position && bounds.Contains(position);
                bool pressed = option.Enabled && pressedId == option.Id;
                controls.Add(new(option.Id, option.Label, option.Kind, bounds, selected, hovered, pressed,
                    Enabled: option.Enabled, Locked: option.Locked));
                panels.Add(new(bounds, new(1, 1, 1), MenuPanelKind.Control));
                if (selected) panels.Add(new(Rect(104, y + 8, 3, 24), Accent, MenuPanelKind.Focus));
                Label(option.Label, 124, y, 40, 23);
                if (selected) Label(">", 434, y, 40, 23, Accent);
            }
            return new(screen, new(width, height), controls, panels, texts);
        }

        if (screen == AppScreen.Playing && hud is not null)
        {
            panels.Add(new(Rect(24f, 24f, 336f, 108f), new(0.025f, 0.04f, 0.07f, 0.85f), MenuPanelKind.Sheet));
            Text(hud.LevelName, 44f, 38f, 22f);
            Text($"Coins {hud.CoinCount}/{hud.TotalCoins}", 44f, 72f, 18f);
            Text(hud.BoostText, 44f, 100f, 16f, Accent);
            return new(screen, new(width, height), controls, panels, texts);
        }

        // Полупрозрачное затемнение сохраняет игровую сцену под паузой и результатом.
        panels.Add(new(new(0f, 0f, width, height), new(0.015f, 0.025f, 0.045f, 0.65f), MenuPanelKind.Backdrop));
        panels.Add(new(Rect(340f, 60f, 600f, 600f), new(0.035f, 0.055f, 0.09f, 0.94f), MenuPanelKind.Sheet));
        Text(page.Title, 376f, 92f, 34f);
        for (int i = 0; i < page.Details.Count; i++) Text(page.Details[i], 376f,
            152f + (page.IsControlsPage ? 32f : 30f) * i, page.IsControlsPage ? 20f : 22f, Muted);

        float row = MathF.Min(64f, 320f / Math.Max(1, page.Options.Count));
        for (int i = 0; i < page.Options.Count; i++)
        {
            var option = page.Options[i];
            bool skinGrid = screen == AppScreen.SkinSelect;
            bool levels = screen == AppScreen.LevelSelect;
            bool settings = screen == AppScreen.Settings;
            int cards = skinGrid ? GameSkins.All.Count : levels ? page.Options.Count - 1 : 0;
            bool card = i < cards;
            bool levelGrid = levels && cards > 5;
            float levelRow = MathF.Min(68f, 340f / Math.Max(1, (cards + 1) / 2));
            float skinRow = MathF.Min(57f, 340f / Math.Max(1, (cards + 1) / 2));
            bool paired = (skinGrid || levelGrid) && card;
            float x = paired ? 376f + (card ? i : i - cards) % 2 * 264f : 376f;
            float y = skinGrid ? (card ? 210f + i / 2 * skinRow : 574f)
                : levelGrid ? (card ? 210f + i / 2 * levelRow : 574f)
                : levels ? (card ? 210f + i * 54f : 218f + cards * 54f)
                : page.IsControlsPage ? 574f : settings ? 224f + 50f * i : 238f + row * i;
            float buttonHeight = skinGrid ? (card ? skinRow - 7f : 40f) : levelGrid ? (card ? levelRow - 10f : 40f) : levels ? (card ? 46f : 40f)
                : settings ? 44f : row - 12;
            var bounds = Rect(x, y, paired ? 252f : 528f, buttonHeight);
            bool selected = selectedIndex == i;
            bool hovered = pointer is { } position && bounds.Contains(position);
            bool pressed = option.Enabled && option.Id == pressedId;
            var background = pressed ? new ColorRgba(0.1f, 0.32f, 0.5f)
                : selected || hovered ? new ColorRgba(0.12f, 0.23f, 0.36f) : new ColorRgba(0.075f, 0.11f, 0.17f);
            panels.Add(new(bounds, background, MenuPanelKind.Control));
            if (selected) panels.Add(new(Rect(x, y, 4f, buttonHeight), Accent, MenuPanelKind.Focus));
            MenuRect? valueBounds = option.Kind == MenuControlKind.Volume
                ? Rect(396f, y + buttonHeight - 9f, 488f, 3f) : null;
            float previewSize = MathF.Min(44, buttonHeight - 6);
            MenuRect? previewBounds = option.Kind == MenuControlKind.Skin ? Rect(x + 6, y + 3, previewSize, previewSize) : null;
            controls.Add(new(option.Id, option.Label, option.Kind, bounds, selected, hovered, pressed,
                valueBounds, previewBounds, option.Enabled, option.Locked));
            ColorRgba ink = option.Enabled && !option.Locked ? White : Muted;
            if (previewBounds is not null)
            {
                CardText(GameSkins.Get(option.Value!).DisplayName, option.Detail ?? "", x + 60, y, buttonHeight,
                    3, 24, 17, 12, ink, option.Locked ? Muted : Accent);
                if (option.Swatch is { } swatch) panels.Add(new(Rect(x + 60, y + buttonHeight - 4, 24, 2), swatch));
            }
            else if (option.Kind == MenuControlKind.Level)
            {
                CardText(option.Label, option.Detail ?? "", x + 14, y, buttonHeight,
                    3, levelGrid ? 25 : 26, levelGrid ? 17 : 19, levelGrid ? 12 : 14,
                    ink, LevelDifficulties.Color(option.Difficulty ?? LevelDifficulty.Normal), MenuTextKind.Difficulty);
                for (int star = 0; star < 3; star++)
                    images.Add(new(Rect(x + (levelGrid ? 184 : 430) + star * (levelGrid ? 19 : 28), y + 8,
                        levelGrid ? 17 : 25, levelGrid ? 17 : 25),
                        star < (option.Rating ?? 0) ? MenuArtwork.FilledStar : MenuArtwork.EmptyStar));
            }
            else if (option.Kind == MenuControlKind.Volume)
                Label(option.Label, x + 20, y, buttonHeight, 20f, ink);
            else Label(option.Label, x + 20, y, buttonHeight, settings ? 20f : 22f, ink);
            if (valueBounds is { } track)
            {
                panels.Add(new(track, new(0.2f, 0.25f, 0.33f), MenuPanelKind.Track));
                panels.Add(new(track with { Width = track.Width * volume }, Accent, MenuPanelKind.Fill));
            }
        }
        return new(screen, new(width, height), controls, panels, texts, images);
    }
}
