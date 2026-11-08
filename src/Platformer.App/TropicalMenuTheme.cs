using System.Numerics;
using Engine3D.Core;

namespace Platformer.App;

/// <summary>Неизменяемые процедурные материалы меню; создаются один раз, без файлов и GPU API.</summary>
internal static class TropicalMenuTheme
{
    private static readonly ColorRgba Ink = new(0.94f, 0.98f, 1f);
    private static readonly ColorRgba SecondaryInk = new(0.72f, 0.85f, 0.87f);
    private static readonly ColorRgba Teal = new(0.42f, 0.96f, 0.80f);
    private static readonly TextureData Pixel = new(1, 1, [255, 255, 255, 255]);
    private static readonly TextureData Backdrop = SideShade();
    private static readonly TextureData Sheet = Rounded(360, 360, new(0.035f, 0.13f, 0.17f), new(0.025f, 0.08f, 0.12f), 12, 1, new(0.19f, 0.40f, 0.41f));
    private static readonly TextureData Button = Rounded(320, 64, new(0.06f, 0.19f, 0.23f), new(0.035f, 0.12f, 0.17f), 8, 1, new(0.17f, 0.35f, 0.39f));
    private static readonly TextureData Selected = Rounded(320, 64, new(0.05f, 0.32f, 0.33f), new(0.025f, 0.22f, 0.27f), 8, 2, new(0.42f, 0.96f, 0.80f));
    private static readonly TextureData Hover = Rounded(320, 64, new(0.06f, 0.26f, 0.29f), new(0.03f, 0.18f, 0.23f), 8, 1, new(0.32f, 0.70f, 0.64f));
    private static readonly TextureData Pressed = Rounded(320, 64, new(0.025f, 0.19f, 0.22f), new(0.02f, 0.12f, 0.18f), 8, 2, new(0.98f, 0.76f, 0.34f));
    private static readonly TextureData Locked = Rounded(320, 64, new(0.10f, 0.15f, 0.19f), new(0.07f, 0.11f, 0.15f), 8, 1, new(0.20f, 0.29f, 0.33f));
    public static OverlayImage Panel(MenuPanel panel, MenuControl? control, bool mainMenu)
    {
        var texture = panel.Kind switch
        {
            MenuPanelKind.Backdrop when mainMenu => Backdrop,
            MenuPanelKind.Sheet => Sheet,
            MenuPanelKind.Control => control is { Enabled: false } or { Locked: true } ? Locked
                : control is { Pressed: true } ? Pressed
                : control is { Selected: true } ? Selected
                : control is { Hovered: true } ? Hover : Button,
            _ => Pixel,
        };
        var tint = panel.Kind switch
        {
            MenuPanelKind.Backdrop => mainMenu ? new ColorRgba(1, 1, 1) : new ColorRgba(0.015f, 0.055f, 0.08f, 0.30f),
            MenuPanelKind.Track => new ColorRgba(0.12f, 0.27f, 0.31f),
            MenuPanelKind.Fill => Teal,
            MenuPanelKind.Focus => Teal,
            MenuPanelKind.Flat => panel.Color,
            _ => new ColorRgba(1, 1, 1),
        };
        var b = panel.Bounds;
        return new(new(b.X, b.Y, b.Width, b.Height), texture, tint);
    }

    public static ColorRgba TextColor(MenuText text)
    {
        if (text.Color.B > 0.8f && text.Color.R < 0.3f) return Teal;
        return text.Color.R > 0.85f ? Ink : SecondaryInk;
    }

    private static TextureData Rounded(int width, int height, Vector3 first, Vector3 last, float radius, float border, Vector3 outline)
    {
        var pixels = new byte[width * height * 4];
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            Vector2 point = new(MathF.Abs(x + 0.5f - width / 2f) - (width / 2f - radius),
                MathF.Abs(y + 0.5f - height / 2f) - (height / 2f - radius));
            float distance = Vector2.Max(point, Vector2.Zero).Length() + MathF.Min(MathF.Max(point.X, point.Y), 0) - radius;
            float alpha = Math.Clamp(0.5f - distance, 0, 1) * 0.94f;
            Vector3 color = distance > -border ? outline : Vector3.Lerp(first, last, y / (float)height);
            int at = (y * width + x) * 4;
            pixels[at] = (byte)(255 * color.X);
            pixels[at + 1] = (byte)(255 * color.Y);
            pixels[at + 2] = (byte)(255 * color.Z);
            pixels[at + 3] = (byte)(255 * alpha);
        }
        return new(width, height, pixels);
    }

    private static TextureData SideShade()
    {
        const int width = 256;
        var pixels = new byte[width * 4];
        for (int x = 0; x < width; x++)
        {
            float fade = 1 - Math.Clamp((x / (float)(width - 1) - 0.22f) / 0.52f, 0, 1);
            pixels[x * 4] = 5;
            pixels[x * 4 + 1] = 22;
            pixels[x * 4 + 2] = 32;
            pixels[x * 4 + 3] = (byte)(235 * fade);
        }
        return new(width, 1, pixels);
    }
}