using System.Text.Json;
using Platformer.Core;

namespace Platformer.App;

public sealed record StoredSettings(string SkinId, float MasterVolume, bool Muted, string MusicTrackId,
    WindowResolution WindowedResolution, bool Fullscreen)
{
    public static StoredSettings Default { get; } = new("classic-blue", 1f, false, MusicTracks.OriginalId, new(960, 640), false);
    public UserSettings CreateUserSettings() => new()
    {
        SkinId = SkinId, MasterVolume = MasterVolume, Muted = Muted, MusicTrackId = MusicTrackId,
    };
    public static StoredSettings Capture(UserSettings user, WindowSettings window) => new(user.SkinId,
        user.MasterVolume, user.Muted, user.MusicTrackId, window.WindowedActual, window.ActualFullscreen);
}

/// <summary>Личный профиль рядом с progress-v1.json; повреждение настроек не затрагивает звёзды.</summary>
public sealed class SettingsStore
{
    private sealed record SaveData(int Version, string SkinId, float MasterVolume, bool Muted, string MusicTrackId,
        int WindowWidth, int WindowHeight, bool Fullscreen);
    private readonly string path;
    public string? Diagnostic { get; private set; }
    public static string DefaultPath => Path.Combine(Path.GetDirectoryName(ProgressStore.DefaultPath)!, "settings-v1.json");

    public SettingsStore(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        this.path = Path.GetFullPath(path);
    }

    public StoredSettings Load(PlayerProgress progress)
    {
        ArgumentNullException.ThrowIfNull(progress);
        Diagnostic = null;
        var defaults = StoredSettings.Default;
        try
        {
            if (!File.Exists(path)) return defaults;
            if (new FileInfo(path).Length > 64 * 1024) throw new InvalidDataException("Файл настроек слишком велик");
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("Version", out var version)
                || version.ValueKind != JsonValueKind.Number || !version.TryGetInt32(out int number) || number != 1)
                throw new InvalidDataException("Неизвестный формат настроек");
            string Text(string key, string fallback) => root.TryGetProperty(key, out var value)
                && value.ValueKind == JsonValueKind.String ? value.GetString()! : fallback;
            bool Flag(string key) => root.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.True;
            var skin = Text("SkinId", defaults.SkinId);
            if (!GameSkins.All.Any(item => item.Id == skin) || !progress.IsSkinUnlocked(skin)) skin = defaults.SkinId;
            var music = Text("MusicTrackId", defaults.MusicTrackId);
            if (!MusicTracks.All.Any(item => item.Id == music)) music = defaults.MusicTrackId;
            float volume = root.TryGetProperty("MasterVolume", out var gain) && gain.ValueKind == JsonValueKind.Number
                && gain.TryGetSingle(out float saved) && float.IsFinite(saved) && saved is >= 0 and <= 1 ? saved : defaults.MasterVolume;
            var size = defaults.WindowedResolution;
            if (root.TryGetProperty("WindowWidth", out var width) && width.ValueKind == JsonValueKind.Number
                && width.TryGetInt32(out int w) && w is > 0 and <= 16384
                && root.TryGetProperty("WindowHeight", out var height) && height.ValueKind == JsonValueKind.Number
                && height.TryGetInt32(out int h) && h is > 0 and <= 16384) size = new(w, h);
            return new(skin, volume, Flag("Muted"), music, size, Flag("Fullscreen"));
        }
        catch (Exception error) when (IsStorageError(error))
        {
            Diagnostic = $"Settings: {error.Message}";
            return defaults;
        }
    }

    public bool Save(StoredSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        Diagnostic = null;
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            byte[] json = JsonSerializer.SerializeToUtf8Bytes(new SaveData(1, settings.SkinId, settings.MasterVolume,
                settings.Muted, settings.MusicTrackId, settings.WindowedResolution.Width, settings.WindowedResolution.Height, settings.Fullscreen));
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
            Diagnostic = $"Settings: {error.Message}";
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

/// <summary>Сохраняет после короткой паузы во вводе; Flush завершает запись при закрытии окна.</summary>
public sealed class SettingsPersistence(SettingsStore store, StoredSettings initial, Action<string>? diagnostic = null)
{
    private StoredSettings observed = initial;
    private bool dirty;
    private double quietSeconds;

    public void Observe(UserSettings user, WindowSettings window, double elapsedSeconds)
    {
        if (!double.IsFinite(elapsedSeconds) || elapsedSeconds < 0) throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
        var current = StoredSettings.Capture(user, window);
        if (current != observed)
        {
            observed = current;
            dirty = true;
            quietSeconds = 0;
        }
        else if (dirty)
        {
            quietSeconds += elapsedSeconds;
            if (quietSeconds >= 0.5) Flush();
        }
    }

    public void Flush()
    {
        if (!dirty) return;
        dirty = false; // Ошибка не должна повторять файловую операцию/сообщение каждый кадр.
        if (!store.Save(observed)) diagnostic?.Invoke(store.Diagnostic ?? "Settings: не удалось сохранить настройки");
    }
}
