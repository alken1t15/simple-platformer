using System.Collections.ObjectModel;
using Platformer.Core;

namespace Platformer.App;

/// <summary>Лучшие завершённые попытки; не зависит от текущего маршрута LevelFlow.</summary>
public sealed class PlayerProgress
{
    private readonly Dictionary<string, int> best = new(StringComparer.Ordinal);
    private readonly LevelCatalog catalog;
    public IReadOnlyDictionary<string, int> BestCoins { get; }
    public int TotalStars => catalog.Levels.Sum(level => StarsFor(level.Id));
    public int BaseStars => catalog.Levels.Where(level => level.Id != GameLevels.FinalLevelId).Sum(level => StarsFor(level.Id));

    public PlayerProgress(LevelCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        this.catalog = catalog;
        BestCoins = new ReadOnlyDictionary<string, int>(best);
    }

    public static int Rating(int coins, int total)
    {
        if (total < 0 || coins < 0 || coins > total) throw new ArgumentOutOfRangeException(nameof(coins));
        return coins == total ? 3 : coins >= (total + 1) / 2 ? 2 : 1;
    }

    public int StarsFor(string levelId) => best.TryGetValue(levelId, out int coins)
        ? Rating(coins, catalog.Get(levelId).Coins.Count) : 0;

    public bool IsLevelUnlocked(string levelId)
    {
        catalog.Get(levelId);
        return levelId != GameLevels.FinalLevelId || BaseStars >= 12;
    }

    public bool IsSkinUnlocked(string skinId) => TotalStars >= GameSkins.Get(skinId).RequiredStars;

    /// <summary>Вызывается только при успешно завершённом уровне; возвращает изменение рекорда.</summary>
    public bool RecordCompletion(string levelId, int coins)
    {
        var level = catalog.Get(levelId);
        Rating(coins, level.Coins.Count);
        if (!IsLevelUnlocked(levelId)) throw new InvalidOperationException("Уровень ещё не открыт");
        if (best.TryGetValue(levelId, out int previous) && previous >= coins) return false;
        best[levelId] = coins;
        return true;
    }
}
