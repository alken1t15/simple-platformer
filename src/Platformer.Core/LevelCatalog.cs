namespace Platformer.Core;

/// <summary>Неизменяемый каталог; порядок записей задаёт маршрут прохождения.</summary>
public sealed class LevelCatalog
{
    private readonly Dictionary<string, int> indices = new(StringComparer.Ordinal);
    public IReadOnlyList<LevelDefinition> Levels { get; }

    public LevelCatalog(IReadOnlyList<LevelDefinition> levels)
    {
        ArgumentNullException.ThrowIfNull(levels);
        if (levels.Count == 0) throw new ArgumentException("Каталог не должен быть пустым", nameof(levels));
        var copy = levels.ToArray();
        for (int i = 0; i < copy.Length; i++)
        {
            if (copy[i] is null) throw new ArgumentException("Уровень не должен быть null", nameof(levels));
            if (!indices.TryAdd(copy[i].Id, i))
                throw new ArgumentException($"Повторный уровень '{copy[i].Id}'", nameof(levels));
        }
        Levels = Array.AsReadOnly(copy);
    }

    public LevelDefinition Get(string id) => Levels[Index(id)];
    public string? GetNextId(string id)
    {
        int next = Index(id) + 1;
        return next < Levels.Count ? Levels[next].Id : null;
    }

    private int Index(string id)
    {
        LevelValidation.Id(id, nameof(id));
        return indices.TryGetValue(id, out int index) ? index
            : throw new KeyNotFoundException($"Неизвестный уровень '{id}'");
    }
}
