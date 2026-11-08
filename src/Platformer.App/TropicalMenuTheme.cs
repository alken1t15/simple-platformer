using System.Numerics;
using Engine3D.Core;

namespace Platformer.App;

/// <summary>Неизменяемые процедурные материалы меню; создаются один раз, без файлов и GPU API.</summary>
internal static class TropicalMenuTheme
{
    private static readonly ColorRgba Ink = new(0.09f, 0.22f, 0.25f);
    private static readonly ColorRgba SecondaryInk = new(0.28f, 0.40f, 0.42f);
    private static readonly ColorRgba Teal = new(0.02f, 0.38f, 0.39f);
    private static readonly TextureData Pixel = new(1, 1, [255, 255, 255, 255]);
    private static readonly TextureData Sheet = Rounded(360, 360, new(1f, 0.98f, 0.88f), new(0.96f, 0.93f, 0.82f), 22, 2, new(0.20f, 0.54f, 0.51f));
    private static readonly TextureData Button = Rounded(320, 64, new(0.91f, 0.96f, 0.88f), new(0.82f, 0.91f, 0.84f), 12, 2, new(0.65f, 0.80f, 0.73f));
    private static readonly TextureData Selected = Rounded(320, 64, new(1f, 0.84f, 0.48f), new(1f, 0.70f, 0.35f), 12, 3, new(0.84f, 0.35f, 0.26f));
    private static readonly TextureData Hover = Rounded(320, 64, new(0.82f, 0.96f, 0.88f), new(0.68f, 0.87f, 0.79f), 12, 2, new(0.07f, 0.52f, 0.48f));
    private static readonly TextureData Pressed = Rounded(320, 64, new(1f, 0.69f, 0.45f), new(0.96f, 0.54f, 0.38f), 12, 3, new(0.76f, 0.28f, 0.22f));
    private static readonly TextureData Locked = Rounded(320, 64, new(0.88f, 0.88f, 0.81f), new(0.82f, 0.84f, 0.78f), 12, 2, new(0.73f, 0.76f, 0.70f));
    private static readonly TextureData Checker = CheckerStripe();

    public static OverlayImage Panel(MenuPanel panel, MenuControl? control)
    {
        var texture = panel.Kind switch
        {
            MenuPanelKind.Sheet => Sheet,
            MenuPanelKind.Control => control is { Enabled: false } or { Locked: true } ? Locked
                : control is { Pressed: true } ? Pressed
                : control is { Selected: true } ? Selected
                : control is { Hovered: true } ? Hover : Button,
            _ => Pixel,
        };
        var tint = panel.Kind switch
        {
            MenuPanelKind.Backdrop => new ColorRgba(0.08f, 0.28f, 0.30f, 0.18f),
            MenuPanelKind.Track => new ColorRgba(0.52f, 0.68f, 0.61f),
            MenuPanelKind.Fill => new ColorRgba(0.02f, 0.49f, 0.44f),
            MenuPanelKind.Focus => new ColorRgba(0.85f, 0.29f, 0.20f),
            MenuPanelKind.Flat => panel.Color,
            _ => new ColorRgba(1, 1, 1),
        };
        var b = panel.Bounds;
        return new(new(b.X, b.Y, b.Width, b.Height), texture, tint);
    }

    public static IEnumerable<OverlayImage> Decoration(MenuView view)
    {
        if (view.Screen == AppScreen.Playing || view.ClientSize.X <= 0 || view.ClientSize.Y <= 0) yield break;
        float scale = MathF.Min(view.ClientSize.X / 1280, view.ClientSize.Y / 720);
        float left = (view.ClientSize.X - 1280 * scale) / 2, top = (view.ClientSize.Y - 720 * scale) / 2;
        // Шахматный мотив дороги не перекрывает ни текст, ни интерактивные карточки.
        yield return new(new(left + 376 * scale, top + 126 * scale, 168 * scale, 8 * scale), Checker, new(1, 1, 1));
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
            float alpha = Math.Clamp(0.5f - distance, 0, 1);
            Vector3 color = distance > -border ? outline : Vector3.Lerp(first, last, y / (float)height);
            int at = (y * width + x) * 4;
            pixels[at] = (byte)(255 * color.X);
            pixels[at + 1] = (byte)(255 * color.Y);
            pixels[at + 2] = (byte)(255 * color.Z);
            pixels[at + 3] = (byte)(255 * alpha);
        }
        return new(width, height, pixels);
    }

    private static TextureData CheckerStripe()
    {
        const int width = 168, height = 8;
        var pixels = new byte[width * height * 4];
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            bool coral = ((x / 8 + y / 4) & 1) == 0;
            int at = (y * width + x) * 4;
            pixels[at] = coral ? (byte)247 : (byte)255;
            pixels[at + 1] = coral ? (byte)109 : (byte)232;
            pixels[at + 2] = coral ? (byte)118 : (byte)191;
            pixels[at + 3] = 255;
        }
        return new(width, height, pixels);
    }
}
