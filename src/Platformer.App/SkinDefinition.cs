using Engine3D.Core;

namespace Platformer.App;

/// <summary>Только внешний вид: радиус, масса и другие физические настройки отсутствуют.</summary>
public sealed class SkinDefinition
{
    private readonly Lazy<TextureData> preview;
    public string Id { get; }
    public string DisplayName { get; }
    public MaterialData Material { get; }
    public ColorRgba SwatchColor { get; }
    public int RequiredStars { get; }
    public TextureData Preview => preview.Value;

    public SkinDefinition(string id, string displayName, MaterialData material, ColorRgba? swatchColor = null, int requiredStars = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentNullException.ThrowIfNull(material);
        material.Validate();
        Id = id;
        DisplayName = displayName;
        Material = material;
        ArgumentOutOfRangeException.ThrowIfNegative(requiredStars);
        RequiredStars = requiredStars;
        SwatchColor = swatchColor ?? material.BaseColor;
        if (!new[] { SwatchColor.R, SwatchColor.G, SwatchColor.B, SwatchColor.A }
            .All(c => float.IsFinite(c) && c >= 0 && c <= 1))
            throw new ArgumentException("Некорректный цвет образца", nameof(swatchColor));
        preview = new(() => SkinTextures.Preview(Material));
    }
}

public static class GameSkins
{
    public static IReadOnlyList<SkinDefinition> All { get; } = Create();

    public static SkinDefinition Get(string id) => All.FirstOrDefault(skin => skin.Id == id)
        ?? throw new KeyNotFoundException($"Неизвестный скин {id}");

    private static IReadOnlyList<SkinDefinition> Create()
    {
        const int width = 64, height = 32;
        var pixels = new byte[width * height * 4];
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            // Меридианы и асимметричный пояс делают качение заметным вокруг разных осей
            byte shade = x % 16 < 3 || y is >= 10 and <= 12 ? (byte)40 : (byte)255;
            int at = (y * width + x) * 4;
            pixels[at] = pixels[at + 1] = pixels[at + 2] = shade;
            pixels[at + 3] = 255;
        }
        var texture = new TextureData(width, height, pixels);
        return Array.AsReadOnly<SkinDefinition>(
        [
            new("classic-blue", "Classic blue", new MaterialData { BaseColor = new ColorRgba(0.12f, 0.48f, 1f), Texture = texture }),
            new("orange", "Orange", new MaterialData { BaseColor = new ColorRgba(1f, 0.38f, 0.06f), Texture = texture }),
            new("emerald", "Emerald stars", new MaterialData { Texture = SkinTextures.StarBall(new(75, 246, 107)) }, new(0.29f, 0.96f, 0.42f)),
            new("violet", "Violet stars", new MaterialData { Texture = SkinTextures.StarBall(new(177, 111, 250)) }, new(0.69f, 0.44f, 0.98f)),
            new("basketball", "Basketball", new MaterialData { Texture = SkinTextures.Basketball() }, new(0.95f, 0.52f, 0.16f)),
            new("football", "Football", new MaterialData { Texture = SkinTextures.Football() }, new(0.98f, 0.98f, 0.92f)),
            new("neon", "Neon circuit", new MaterialData { Texture = SkinTextures.RewardBall(0) }, new(0.2f, 1f, 0.9f), 6),
            new("aurora", "Aurora", new MaterialData { Texture = SkinTextures.RewardBall(1) }, new(0.7f, 0.4f, 1f), 9),
            new("champion", "Golden champion", new MaterialData { Texture = SkinTextures.RewardBall(2) }, new(1f, 0.8f, 0.15f), 15),
            new("watermelon", "Watermelon", new MaterialData { Texture = SkinTextures.Watermelon() }, new(0.95f, 0.35f, 0.39f)),
            new("porcelain", "Porcelain wave", new MaterialData { Texture = SkinTextures.Porcelain() }, new(0.34f, 0.57f, 0.86f)),
            new("magma", "Magma core", new MaterialData { Texture = SkinTextures.Magma() }, new(1f, 0.43f, 0.15f)),
            new("disco", "Disco mosaic", new MaterialData { Texture = SkinTextures.Disco() }, new(0.86f, 0.57f, 0.84f)),
            new("voyager", "Star voyager", new MaterialData { Texture = SkinTextures.Voyager() }, new(0.39f, 0.87f, 0.86f)),
        ]);
    }
}
