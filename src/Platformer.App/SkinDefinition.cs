using Engine3D.Core;

namespace Platformer.App;

/// <summary>Только внешний вид: радиус, масса и другие физические настройки отсутствуют.</summary>
public sealed class SkinDefinition
{
    public string Id { get; }
    public string DisplayName { get; }
    public MaterialData Material { get; }

    public SkinDefinition(string id, string displayName, MaterialData material)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentNullException.ThrowIfNull(material);
        material.Validate();
        Id = id;
        DisplayName = displayName;
        Material = material;
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
        ]);
    }
}
