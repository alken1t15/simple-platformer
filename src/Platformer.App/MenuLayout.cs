using System.Numerics;
using Engine3D.Core;

namespace Platformer.App;

internal enum MenuAction { Play, Levels, Settings, Skins, Exit, ChooseLevel, Back, Volume, Mute,
    ChooseSkin, Continue, Restart, MainMenu, Next, Resolution, ApplyResolution, DisplayMode, CancelMainMenu, ConfirmMainMenu, Music }
internal sealed record MenuOption(string Id, string Label, MenuAction Action,
    MenuControlKind Kind = MenuControlKind.Button, string? Value = null, ColorRgba? Swatch = null, bool Enabled = true, string? Detail = null, bool Locked = false, int? Rating = null);
internal sealed record MenuPage(string Title, IReadOnlyList<string> Details, IReadOnlyList<MenuOption> Options);

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
        int selectedIndex, Vector2? pointer, string? pressedId, float width, float height)
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
        void Text(string text, float x, float y, float size, ColorRgba? color = null) =>
            texts.Add(new(new(left + x * scale, top + y * scale), text, size * scale, color ?? White));

        if (screen == AppScreen.Playing && hud is not null)
        {
            panels.Add(new(Rect(24f, 24f, 432f, 144f), new(0.025f, 0.04f, 0.07f, 0.85f)));
            Text(hud.LevelName, 44f, 40f, 28f);
            Text($"Coins {hud.CoinCount}/{hud.TotalCoins}", 44f, 84f, 24f);
            Text(hud.BoostText, 44f, 122f, 22f, Accent);
            if (hud.ShowHelp)
            {
                float helpTop = hud.Hint is null ? 614f : 580f;
                panels.Add(new(Rect(24f, helpTop, 1000f, 696f - helpTop), new(0.025f, 0.04f, 0.07f, 0.85f)));
                Text("WASD move   Mouse camera   Space jump", 44f, helpTop + 14, 22f);
                Text("Shift boost   R restart   Esc pause", 44f, helpTop + 48, 22f, Muted);
                if (hud.Hint is not null) Text(hud.Hint, 44f, helpTop + 82, 18f, Accent);
            }
            return new(screen, new(width, height), controls, panels, texts);
        }

        // Полупрозрачное затемнение сохраняет игровую сцену под паузой и результатом.
        panels.Add(new(new(0f, 0f, width, height), new(0.015f, 0.025f, 0.045f, 0.65f)));
        panels.Add(new(Rect(340f, 60f, 600f, 600f), new(0.035f, 0.055f, 0.09f, 0.94f)));
        Text(page.Title, 376f, 92f, 34f);
        for (int i = 0; i < page.Details.Count; i++) Text(page.Details[i], 376f, 152f + 30f * i, 22f, Muted);

        float row = MathF.Min(64f, 320f / Math.Max(1, page.Options.Count));
        for (int i = 0; i < page.Options.Count; i++)
        {
            var option = page.Options[i];
            bool skinGrid = screen == AppScreen.SkinSelect;
            bool levels = screen == AppScreen.LevelSelect;
            int cards = skinGrid ? GameSkins.All.Count : levels ? page.Options.Count - 1 : 0;
            bool card = i < cards;
            bool paired = skinGrid && card;
            float x = paired ? 376f + (card ? i : i - cards) % 2 * 264f : 376f;
            float y = skinGrid ? (card ? 215f + i / 2 * 57f : 510f)
                : levels ? (card ? 210f + i * 54f : 218f + cards * 54f) : 238f + row * i;
            float buttonHeight = skinGrid ? (card ? 50f : 40f) : levels ? (card ? 46f : 40f) : row - 12;
            var bounds = Rect(x, y, paired ? 252f : 528f, buttonHeight);
            bool selected = selectedIndex == i;
            bool hovered = pointer is { } position && bounds.Contains(position);
            bool pressed = option.Enabled && option.Id == pressedId;
            var background = pressed ? new ColorRgba(0.1f, 0.32f, 0.5f)
                : selected || hovered ? new ColorRgba(0.12f, 0.23f, 0.36f) : new ColorRgba(0.075f, 0.11f, 0.17f);
            panels.Add(new(bounds, background));
            if (selected) panels.Add(new(Rect(x, y, 4f, buttonHeight), Accent));
            MenuRect? valueBounds = option.Kind == MenuControlKind.Volume
                ? Rect(396f, y + buttonHeight - 9f, 488f, 3f) : null;
            MenuRect? previewBounds = option.Kind == MenuControlKind.Skin ? Rect(x + 6, y + 3, 44, 44) : null;
            controls.Add(new(option.Id, option.Label, option.Kind, bounds, selected, hovered, pressed,
                valueBounds, previewBounds, option.Enabled, option.Locked));
            ColorRgba ink = option.Enabled && !option.Locked ? White : Muted;
            if (previewBounds is not null)
            {
                Text(GameSkins.Get(option.Value!).DisplayName, x + 60, y + 6, 17, ink);
                Text(option.Detail ?? "", x + 60, y + 29, 13, option.Locked ? Muted : Accent);
                if (option.Swatch is { } swatch) panels.Add(new(Rect(x + 60, y + 46, 24, 2), swatch));
            }
            else if (option.Kind == MenuControlKind.Level)
            {
                Text(option.Label, x + 14, y + 3, 19, ink);
                Text(option.Detail ?? "", x + 14, y + 26, 14, option.Locked ? Muted : Accent);
                for (int star = 0; star < 3; star++)
                    images.Add(new(Rect(x + 430 + star * 28, y + 8, 25, 25),
                        star < (option.Rating ?? 0) ? MenuArtwork.FilledStar : MenuArtwork.EmptyStar));
            }
            else if (option.Kind == MenuControlKind.Volume)
                Text(option.Label, x + 20, y + 1f, 20f, ink); // Семь строк Settings: подпись выше шкалы.
            else Text(option.Label, x + 20, y + 9f, 22f, ink);
            if (valueBounds is { } track)
            {
                panels.Add(new(track, new(0.2f, 0.25f, 0.33f)));
                panels.Add(new(track with { Width = track.Width * volume }, Accent));
            }
        }
        Text(screen == AppScreen.Settings ? "Up / Down: choose; Left / Right: change" : "Up / Down: choose", 376f, 566f, 18f, Muted);
        Text("Tab / Shift+Tab: next / previous", 376f, 588f, 18f, Muted);
        Text("Enter / Space / Mouse click: select", 376f, 610f, 18f, Muted);
        Text(screen == AppScreen.MainMenu ? "Exit: close game" : "Esc: back", 376f, 632f, 18f, Muted);
        return new(screen, new(width, height), controls, panels, texts, images);
    }
}
