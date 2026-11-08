using Engine3D.Audio;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Platformer.App;
using Platformer.Core;

namespace Platformer.Tests.Integration;

/// <summary>
/// Поставляемые WAV через публичный AudioDevice: настоящий backend или silent fallback.
/// Разбор и загрузка проверяются, слышимость — нет (ручная Linux-приёмка #11).
/// </summary>
[TestClass]
public sealed class NativeAudioAssetsIntegrationTests
{
    [TestMethod]
    public void BundledAudioLoadsFromBaseDirectoryThroughPublicAudioDevice()
    {
        // Program строит путь от AppContext.BaseDirectory; LoadWave разбирает WAV и в silent fallback.
        string directory = Path.Combine(AppContext.BaseDirectory, "Assets", "Audio");
        using var device = AudioDevice.Create(new AudioOptions());
        device.MasterVolume = 0f;
        using var playback = new NativeAudioPlayback(device, directory);
        using var coordinator = new AudioCoordinator(playback);
        var session = Guid.NewGuid();
        coordinator.SetScreen(AppScreen.Playing);
        coordinator.Consume(session, [
            new(session, 1, 1, GameEventKind.Jumped, null),
            new(session, 2, 2, GameEventKind.CoinCollected, "coin"),
            new(session, 3, 3, GameEventKind.BoostStarted, null),
            new(session, 4, 4, GameEventKind.Failed, null),
            new(session, 5, 5, GameEventKind.LevelCompleted, null)]);
        coordinator.SetScreen(AppScreen.Paused);
        coordinator.SetScreen(AppScreen.Playing);
        coordinator.Stop();
        // Каждый клип backend запускается напрямую: ни один WAV не пропущен из-за маршрутизации событий.
        foreach (var kind in new[] { GameEventKind.Jumped, GameEventKind.CoinCollected, GameEventKind.Failed, GameEventKind.LevelCompleted })
            using (playback.PlayEffect(kind)) { }
        using (playback.PlayMusic()) { }
    }

    [TestMethod]
    public void BundledAudioShipsLicenseNextToAssets() =>
        Assert.IsTrue(File.Exists(Path.Combine(AppContext.BaseDirectory, "Assets", "Audio", "LICENSE.txt")),
            "Лицензия звуков поставляется рядом с ассетами");

    [TestMethod]
    public void MissingAudioFileFailsPlaybackConstructionWithFileName()
    {
        string directory = Directory.CreateTempSubdirectory("platformer-audio-").FullName;
        try
        {
            File.Copy(Path.Combine(AppContext.BaseDirectory, "Assets", "Audio", "music.wav"),
                Path.Combine(directory, "music.wav"));
            using var device = AudioDevice.Create(new AudioOptions());
            var error = Assert.ThrowsExactly<FileNotFoundException>(() => new NativeAudioPlayback(device, directory));
            StringAssert.Contains(error.Message + error.FileName, "jump.wav");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
