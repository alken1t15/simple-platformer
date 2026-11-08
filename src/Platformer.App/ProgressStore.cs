using System.Text.Json;
using Platformer.Core;

namespace Platformer.App;

/// <summary>Версионированное локальное сохранение; ошибки файлов не прерывают игру.</summary>
public sealed class ProgressStore
{
    private sealed record SaveData(int Version, Dictionary<string, int> BestCoins);
    private readonly string path;
    public string? Diagnostic { get; private set; }
    public static string DefaultPath => Path.Combine(Environment.GetFolderPath(
        Environment.SpecialFolder.LocalApplicationData), "BallAdventure", "progress-v1.json");

    public ProgressStore(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        this.path = Path.GetFullPath(path);
    }

    public PlayerProgress Load(LevelCatalog catalog)
    {
        var progress = new PlayerProgress(catalog);
        Diagnostic = null;
        try
        {
            if (!File.Exists(path)) return progress;
            if (new FileInfo(path).Length > 64 * 1024) throw new InvalidDataException("Файл прогресса слишком велик");
            var data = JsonSerializer.Deserialize<SaveData>(File.ReadAllText(path));
            if (data is not { Version: 1, BestCoins: not null }) throw new InvalidDataException("Неизвестный формат прогресса");
            var levels = catalog.Levels.ToDictionary(level => level.Id, StringComparer.Ordinal);
            // Сначала проверяем весь файл, затем принимаем результаты: частичного импорта нет.
            foreach (var (id, coins) in data.BestCoins)
                if (levels.TryGetValue(id, out var level)) PlayerProgress.Rating(coins, level.Coins.Count);
            foreach (var (id, coins) in data.BestCoins.Where(pair => pair.Key != GameLevels.FinalLevelId))
                if (levels.ContainsKey(id)) progress.RecordCompletion(id, coins);
            if (data.BestCoins.TryGetValue(GameLevels.FinalLevelId, out int finalCoins)
                && levels.ContainsKey(GameLevels.FinalLevelId) && progress.IsLevelUnlocked(GameLevels.FinalLevelId))
                progress.RecordCompletion(GameLevels.FinalLevelId, finalCoins);
        }
        catch (Exception error) when (IsStorageError(error) || error is ArgumentOutOfRangeException)
        {
            Diagnostic = $"Progress: {error.Message}";
            return new PlayerProgress(catalog);
        }
        return progress;
    }

    public bool Save(PlayerProgress progress)
    {
        ArgumentNullException.ThrowIfNull(progress);
        Diagnostic = null;
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            byte[] json = JsonSerializer.SerializeToUtf8Bytes(new SaveData(1, new(progress.BestCoins)));
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(json);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporary, path, overwrite: true);
            return true;
        }
        catch (Exception error) when (IsStorageError(error))
        {
            Diagnostic = $"Progress: {error.Message}";
            return false;
        }
        finally
        {
            try { File.Delete(temporary); }
            catch (Exception error) when (IsStorageError(error)) { }
        }
    }

    private static bool IsStorageError(Exception error) => error is IOException or InvalidDataException or UnauthorizedAccessException or JsonException;
}
