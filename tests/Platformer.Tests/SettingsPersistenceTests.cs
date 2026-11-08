using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Platformer.App;

namespace Platformer.Tests;

[TestClass]
public sealed class SettingsPersistenceTests
{
    private static PlayerProgress Progress() => new(GameLevels.CreateCatalog());

    [TestMethod]
    public void StartupWindowSelectionSurvivesFullscreenAndReturnsToSavedSize()
    {
        var window = new WindowSettings();
        window.Synchronize(1280, 720, 800, 450, 1600, 900, false, 1920, 1080);
        window.SelectActual(); // Та же последовательность, что при запуске из сохранённого профиля.
        window.Synchronize(1920, 1080, 800, 450, 1600, 900, true, 1920, 1080);
        window.SelectActual();
        window.ToggleMode();
        window.RequestApply();
        Assert.AreEqual(new WindowDisplayRequest(new(1280, 720), false), window.TakeDisplayRequest());
    }

    [TestMethod]
    public void RestartRestoresSkinAudioAndWindowedSizeWhileProgressStaysSeparate()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "settings.json");
            var progress = Progress();
            var progressPath = Path.Combine(directory, "progress.json");
            new ProgressStore(progressPath).Save(progress);
            string originalProgress = File.ReadAllText(progressPath);
            var expected = new StoredSettings("basketball", 0.35f, true, MusicTracks.ExplorationId, new(1280, 720), true);
            Assert.IsTrue(new SettingsStore(path).Save(expected));
            Assert.AreEqual(expected, new SettingsStore(path).Load(progress));
            var user = new SettingsStore(path).Load(progress).CreateUserSettings();
            Assert.AreEqual(0.35f, user.MasterVolume); // Mute не уничтожает громкость для Unmute.
            Assert.IsTrue(user.Muted);
            Assert.AreEqual(originalProgress, File.ReadAllText(progressPath));
            Assert.HasCount(2, Directory.GetFiles(directory));
            var locked = expected with { SkinId = "champion" };
            new SettingsStore(path).Save(locked);
            Assert.AreEqual("classic-blue", new SettingsStore(path).Load(progress).SkinId);
            foreach (var level in GameLevels.CreateCatalog().Levels) progress.RecordCompletion(level.Id, level.Coins.Count);
            Assert.AreEqual("champion", new SettingsStore(path).Load(progress).SkinId);
        }
        finally { Directory.Delete(directory, true); }
    }

    [TestMethod]
    public void InvalidProfileUsesDefaultsAndInvalidFieldsDoNotEraseValidFields()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "settings.json");
            var store = new SettingsStore(path);
            Assert.AreEqual(StoredSettings.Default, store.Load(Progress()));
            foreach (string data in new[] { "{", "null", "[]", "{\"Version\":2}", "{\"Version\":\"one\"}", new string('x', 65537) })
            {
                File.WriteAllText(path, data);
                Assert.AreEqual(StoredSettings.Default, store.Load(Progress()));
                Assert.IsNotNull(store.Diagnostic);
            }
            File.WriteAllText(path, """
                {"Version":1,"SkinId":"unknown","MasterVolume":0.4,"Muted":true,"MusicTrackId":"gone","WindowWidth":-1,"WindowHeight":900}
                """);
            var loaded = store.Load(Progress());
            Assert.AreEqual(StoredSettings.Default with { MasterVolume = 0.4f, Muted = true }, loaded);
            var blocked = new SettingsStore(Path.Combine(path, "child.json"));
            Assert.IsFalse(blocked.Save(loaded));
            Assert.IsNotNull(blocked.Diagnostic);
            Assert.IsTrue(File.ReadAllText(path).Contains("0.4", StringComparison.Ordinal));
        }
        finally { Directory.Delete(directory, true); }
    }

    [TestMethod]
    public void OnlyConfirmedDisplayIsSavedAndUnappliedSelectionDoesNotChangeProfile()
    {
        var user = new UserSettings();
        var window = new WindowSettings();
        window.Synchronize(1280, 720, 800, 450, 1600, 900, false, 1920, 1080);
        window.SelectActual();
        var before = StoredSettings.Capture(user, window);
        window.Cycle(1);
        window.RequestApply();
        Assert.AreEqual(before, StoredSettings.Capture(user, window));
        window.TakeDisplayRequest(); // Запрос ещё не является подтверждением ОС.
        Assert.AreEqual(before, StoredSettings.Capture(user, window));
        window.Synchronize(1600, 900, 800, 450, 1600, 900);
        window.Synchronize(1920, 1080, 800, 450, 1600, 900, true, 1920, 1080);
        Assert.AreEqual(new WindowResolution(1600, 900), StoredSettings.Capture(user, window).WindowedResolution);
        Assert.IsTrue(StoredSettings.Capture(user, window).Fullscreen);
    }

    [TestMethod]
    public void WritesAreDebouncedAndFlushCatchesImmediateExitWithoutRepeatingIoFailures()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "settings.json");
            var store = new SettingsStore(path);
            var user = new UserSettings();
            var window = new WindowSettings();
            var persistence = new SettingsPersistence(store, StoredSettings.Default);
            for (int i = 0; i < 30; i++) persistence.Observe(user, window, 0.1);
            Assert.IsFalse(File.Exists(path));
            user.MasterVolume = 0.7f;
            persistence.Observe(user, window, 0.1);
            Assert.IsFalse(File.Exists(path));
            user.MasterVolume = 0.3f;
            persistence.Observe(user, window, 0.1);
            persistence.Observe(user, window, 0.6);
            Assert.AreEqual(0.3f, store.Load(Progress()).MasterVolume);
            user.SkinId = "orange";
            persistence.Observe(user, window, 0);
            persistence.Flush();
            Assert.AreEqual("orange", store.Load(Progress()).SkinId);
            int diagnostics = 0;
            var blocked = new SettingsPersistence(new SettingsStore(Path.Combine(path, "bad.json")), StoredSettings.Default, _ => diagnostics++);
            blocked.Observe(user, window, 0);
            for (int i = 0; i < 100; i++) blocked.Observe(user, window, 0.1);
            Assert.AreEqual(1, diagnostics);
        }
        finally { Directory.Delete(directory, true); }
    }
}
