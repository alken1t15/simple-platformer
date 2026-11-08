using System.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Platformer.App;

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
        // Пустой runtime-dir без сокета compositor: иначе Wayland найдёт wayland-0 через XDG_RUNTIME_DIR.
        string? runtimeDirectory = withoutDisplay ? Directory.CreateTempSubdirectory("platformer-xdg-").FullName : null;
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
                // Заведомо недоступные адреса только для дочернего процесса; окружение тестовой сессии не меняется.
                start.Environment["DISPLAY"] = ":65535";
                start.Environment["WAYLAND_DISPLAY"] = "platformer-no-wayland";
                start.Environment["XDG_RUNTIME_DIR"] = runtimeDirectory;
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
            if (runtimeDirectory is not null) Directory.Delete(runtimeDirectory, recursive: true);
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
    public void InvalidArgumentsFailFastWithOneReadableLine(string arguments, string message)
    {
        // Без дисплея: ошибка аргументов должна прийти раньше создания окна.
        var run = Launch(arguments.Split(' '), withoutDisplay: true);
        Assert.AreEqual(1, run.ExitCode);
        Assert.AreEqual("", run.Output);
        Assert.AreEqual($"Platformer: {message}", run.Error.TrimEnd());
    }

    [TestMethod]
    public void UnknownSkinFailsBeforeWindowWithNamedValue()
    {
        // Текст сообщения русский; кодировка stderr на Windows не гарантирует кириллицу, проверяем значение.
        var run = Launch(["--presenter", "--skin", "missing", "--smoke"], withoutDisplay: true);
        Assert.AreEqual(1, run.ExitCode);
        StringAssert.StartsWith(run.Error, "Platformer: ");
        StringAssert.Contains(run.Error, "missing");
        Assert.HasCount(1, run.Error.TrimEnd().Split('\n'));
    }

    [TestMethod]
    [DataRow("--smoke")]
    [DataRow("--presenter --smoke")]
    [DataRow("--play --trace-input")]
    public void LaunchWithoutDisplayEndsBoundedWithNonZeroExitAndDiagnostic(string arguments)
    {
        if (!OperatingSystem.IsLinux()) Assert.Inconclusive("Недоступные X11/Wayland моделируются только на Linux");
        var run = Launch(arguments.Split(' '), withoutDisplay: true);
        Assert.AreEqual(1, run.ExitCode, run.Output);
        // Gameplay без --smoke сначала открывает аудио: строки ALSA/OpenAL пишет нативная библиотека.
        string last = run.Error.TrimEnd().Split('\n')[^1];
        StringAssert.StartsWith(last, "Platformer: ");
        StringAssert.Contains(last, "window");
        Assert.DoesNotContain("   at ", run.Error, "Диагностика без stack trace");
    }
}
