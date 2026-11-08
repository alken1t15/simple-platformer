using System.Diagnostics;
using Engine3D.Audio;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Platformer.App;
using Platformer.Core;

namespace Platformer.Tests.Integration;

/// <summary>
/// Процесс Platformer.App из чужого cwd: --help, ошибочные аргументы и ограниченный запуск без дисплея.
/// Рендер --smoke под Xvfb проверяет CI graphics-smoke, здесь окно не создаётся.
/// </summary>
[TestClass]
public sealed class CliProcessIntegrationTests
{
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(60);

    private sealed record Run(int ExitCode, string Output, string Error);

    private static Run Launch(IReadOnlyList<string> arguments, bool withoutDisplay = false)
    {
        string app = typeof(GameplayHost).Assembly.Location;
        string workingDirectory = Directory.CreateTempSubdirectory("platformer-cwd-").FullName;
        try
        {
            var start = new ProcessStartInfo(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet")
            {
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            start.ArgumentList.Add(app);
            foreach (var argument in arguments) start.ArgumentList.Add(argument);
            if (withoutDisplay)
            {
                start.Environment.Remove("DISPLAY");
                start.Environment.Remove("WAYLAND_DISPLAY");
            }
            using var process = Process.Start(start) ?? throw new AssertFailedException("Процесс не запущен");
            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(Limit))
            {
                process.Kill(entireProcessTree: true);
                Assert.Fail($"Platformer.App {string.Join(' ', arguments)} не завершился за {Limit.TotalSeconds} с");
            }
            process.WaitForExit();
            return new(process.ExitCode, output.Result, error.Result);
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    [TestMethod]
    public void HelpFromForeignWorkingDirectoryExitsZeroWithUsage()
    {
        var run = Launch(["--help"]);
        Assert.AreEqual(0, run.ExitCode, run.Error);
        StringAssert.Contains(run.Output, "Usage: Platformer.App");
        StringAssert.Contains(run.Output, "--smoke");
        Assert.AreEqual("", run.Error);
    }

    [TestMethod]
    [DataRow("--bogus", "Unknown arguments. Use --help.")]
    [DataRow("--skin", "Unknown arguments. Use --help.")]
    [DataRow("--help --smoke", "Unknown arguments. Use --help.")]
    [DataRow("--play --legacy", "--play, --legacy and --presenter cannot be combined.")]
    [DataRow("--presenter --play --smoke", "--play, --legacy and --presenter cannot be combined.")]
    [DataRow("--overview", "--skin, --overview and --still require --presenter.")]
    [DataRow("--skin orange", "--skin, --overview and --still require --presenter.")]
    [DataRow("--legacy --trace-input", "--trace-input requires gameplay mode.")]
    [DataRow("--presenter --skin missing --smoke", "Неизвестный скин missing")]
    public void InvalidArgumentsFailFastWithOneReadableLine(string arguments, string message)
    {
        // Без дисплея: ошибка аргументов должна прийти раньше создания окна.
        var run = Launch(arguments.Split(' '), withoutDisplay: true);
        Assert.AreEqual(1, run.ExitCode);
        Assert.AreEqual("", run.Output);
        Assert.AreEqual($"Platformer: {message}", run.Error.TrimEnd());
    }

    [TestMethod]
    [DataRow("--smoke")]
    [DataRow("--presenter --smoke")]
    [DataRow("--play --trace-input")]
    public void LaunchWithoutDisplayEndsBoundedWithNonZeroExitAndDiagnostic(string arguments)
    {
        if (!OperatingSystem.IsLinux()) Assert.Inconclusive("Отсутствие DISPLAY/WAYLAND_DISPLAY моделируется только на Linux");
        var run = Launch(arguments.Split(' '), withoutDisplay: true);
        Assert.AreEqual(1, run.ExitCode, run.Output);
        // Gameplay без --smoke сначала открывает аудио: строки ALSA/OpenAL пишет нативная библиотека.
        string last = run.Error.TrimEnd().Split('\n')[^1];
        StringAssert.StartsWith(last, "Platformer: ");
        StringAssert.Contains(last, "window");
        Assert.DoesNotContain("   at ", run.Error, "Диагностика без stack trace");
    }

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
        Assert.IsTrue(File.Exists(Path.Combine(directory, "LICENSE.txt")), "Лицензия звуков поставляется рядом с ассетами");
    }

    [TestMethod]
    public void MissingAudioFileFailsConstructionAndReleasesLoadedClips()
    {
        string directory = Directory.CreateTempSubdirectory("platformer-audio-").FullName;
        try
        {
            File.Copy(Path.Combine(AppContext.BaseDirectory, "Assets", "Audio", "music.wav"),
                Path.Combine(directory, "music.wav"));
            using var device = AudioDevice.Create(new AudioOptions());
            Assert.ThrowsExactly<FileNotFoundException>(() => new NativeAudioPlayback(device, directory));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
