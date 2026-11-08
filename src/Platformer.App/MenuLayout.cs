using System.Numerics;
using Engine3D.Core;

namespace Platformer.App;

internal enum MenuAction { Play, Levels, Settings, Skins, Exit, ChooseLevel, Start, Back, Volume, Mute,
    ChooseSkin, ApplySkin, Continue, Restart, MainMenu, Next, Resolution, ApplyResolution, DisplayMode }
internal sealed record MenuOption(string Id, string Label, MenuAction Action,
    MenuControlKind Kind = MenuControlKind.Button, string? Value = null, ColorRgba? Swatch = null);
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
                panels.Add(new(Rect(24f, 614f, 830f, 82f), new(0.025f, 0.04f, 0.07f, 0.85f)));
                Text("WASD move   Mouse camera   Space jump", 44f, 628f, 22f);
                Text("Shift boost   R restart   Esc pause", 44f, 662f, 22f, Muted);
            }
            return new(screen, new(width, height), controls, panels, texts);
        }

        // Полупрозрачное затемнение сохраняет игровую сцену под паузой и результатом.
        panels.Add(new(new(0f, 0f, width, height), new(0.015f, 0.025f, 0.045f, 0.65f)));
        panels.Add(new(Rect(340f, 60f, 600f, 600f), new(0.035f, 0.055f, 0.09f, 0.94f)));
        Text(page.Title, 376f, 92f, 34f);
        for (int i = 0; i < page.Details.Count; i++) Text(page.Details[i], 376f, 152f + 30f * i, 22f, Muted);

        // В игре 2 карточки; при расширении каталога сохраняем все элементы в панели.
        float row = MathF.Min(64f, 320f / Math.Max(1, page.Options.Count));
        float buttonHeight = row - 12f;
        for (int i = 0; i < page.Options.Count; i++)
        {
            var option = page.Options[i];
            float y = 238f + row * i;
            var bounds = Rect(376f, y, 528f, buttonHeight);
            bool selected = selectedIndex == i;
            bool hovered = pointer is { } position && bounds.Contains(position);
            bool pressed = option.Id == pressedId;
            var background = pressed ? new ColorRgba(0.1f, 0.32f, 0.5f)
                : selected || hovered ? new ColorRgba(0.12f, 0.23f, 0.36f) : new ColorRgba(0.075f, 0.11f, 0.17f);
            panels.Add(new(bounds, background));
            if (selected) panels.Add(new(Rect(376f, y, 4f, buttonHeight), Accent));
            MenuRect? valueBounds = option.Kind == MenuControlKind.Volume
                ? Rect(396f, y + buttonHeight - 9f, 488f, 3f) : null;
            controls.Add(new(option.Id, option.Label, option.Kind, bounds, selected, hovered, pressed, valueBounds));
            if (option.Swatch is { } color)
            {
                panels.Add(new(Rect(392f, y + 8f, 36f, buttonHeight - 16f), color));
                Text(option.Label, 444f, y + 10f, 22f);
            }
            else Text(option.Label, 396f, y + 9f, 22f);
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
        return new(screen, new(width, height), controls, panels, texts);
    }
}
