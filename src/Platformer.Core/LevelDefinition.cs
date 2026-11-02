using System.Numerics;

namespace Platformer.Core;

/// <summary>Копирует списки; определения объектов и выдаваемые коллекции неизменяемы.</summary>
public sealed class LevelDefinition
{
    public string Id { get; }
    public Vector3 Spawn { get; }
    public float KillY { get; }
    public OrientedBox Finish { get; }
    public IReadOnlyList<SurfaceDefinition> Surfaces { get; }
    public IReadOnlyList<CoinDefinition> Coins { get; }
    public IReadOnlyList<HazardDefinition> Hazards { get; }

    public LevelDefinition(string id, Vector3 spawn, float killY, OrientedBox finish,
        IReadOnlyList<SurfaceDefinition> surfaces, IReadOnlyList<CoinDefinition> coins,
        IReadOnlyList<HazardDefinition> hazards)
    {
        Id = LevelValidation.Id(id, nameof(id));
        LevelValidation.Position(spawn, nameof(spawn));
        LevelValidation.Coordinate(killY, nameof(killY));
        ArgumentNullException.ThrowIfNull(finish);
        Spawn = spawn;
        KillY = killY;
        Finish = finish;
        var ids = new HashSet<string>(StringComparer.Ordinal);
        Surfaces = Copy(surfaces, surface => surface.Id, ids, nameof(surfaces));
        Coins = Copy(coins, coin => coin.Id, ids, nameof(coins));
        Hazards = Copy(hazards, hazard => hazard.Id, ids, nameof(hazards));
    }

    private static IReadOnlyList<T> Copy<T>(IReadOnlyList<T> source, Func<T, string> getId,
        HashSet<string> ids, string parameter) where T : class
    {
        ArgumentNullException.ThrowIfNull(source, parameter);
        var copy = new T[source.Count];
        for (int i = 0; i < copy.Length; i++)
        {
            T item = source[i];
            if (item is null)
                throw new ArgumentException("Коллекция содержит null", parameter);
            string id = getId(item);
            if (!ids.Add(id))
                throw new ArgumentException($"Повторный ID объекта уровня: {id}", parameter);
            copy[i] = item;
        }
        return Array.AsReadOnly(copy);
    }
}
