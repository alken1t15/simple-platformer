using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Platformer.App;
using Platformer.Core;

namespace Platformer.Tests;

[TestClass]
public sealed class MenuKeyboardTests
{
    [TestMethod]
    public void ArrowsAndTabRepeatAfterDelayAndStopOnRelease()
    {
        foreach (var (keys, horizontal, expected) in new[] {
            (AppButtons.Down, false, 1), (AppButtons.Up, false, -1), (AppButtons.Tab, false, 1),
            (AppButtons.Tab | AppButtons.LeftShift, false, -1), (AppButtons.Tab | AppButtons.RightShift, false, -1),
            (AppButtons.Right, true, 1), (AppButtons.Left, true, -1) })
        {
            var router = new InputRouter();
            int Step(AppButtons down, AppButtons pressed, double seconds) {
                var menu = router.Map(new AppInputSnapshot(true, down, pressed), AppScreen.MainMenu, 0, seconds).Menu;
                return horizontal ? menu.HorizontalStep : menu.VerticalStep;
            }
            Assert.AreEqual(expected, Step(keys, keys, 0), keys.ToString());
            Assert.AreEqual(0, Step(keys, 0, 0.2));
            Assert.AreEqual(0, Step(keys, 0, 0.1));
            Assert.AreEqual(expected, Step(keys, 0, 0.06));
            Assert.AreEqual(0, Step(keys, 0, 0.04));
            Assert.AreEqual(expected, Step(keys, 0, 0.04));
            Assert.AreEqual(0, Step(0, 0, 0.25));
            Assert.AreEqual(0, Step(0, 0, 0.25));
            Assert.AreEqual(expected, Step(keys, keys, 0));
            Assert.AreEqual(0, Step(keys, 0, 0.2));
        }
    }

    [TestMethod]
    public void RepeatRateIsStableAcrossFrameRatesAndNeverBurstsAfterAStall()
    {
        int Count(int fps)
        {
            var router = new InputRouter();
            int count = router.Map(new AppInputSnapshot(true, AppButtons.Tab, AppButtons.Tab), AppScreen.MainMenu, 0).Menu.VerticalStep;
            for (int i = 0; i < fps; i++)
                count += router.Map(new AppInputSnapshot(true, AppButtons.Tab), AppScreen.MainMenu, 0, 1d / fps).Menu.VerticalStep;
            Assert.IsTrue(count >= 7 && count <= 11);
            Assert.AreEqual(1, router.Map(new AppInputSnapshot(true, AppButtons.Tab), AppScreen.MainMenu, 0, 60).Menu.VerticalStep);
            Assert.AreEqual(0, router.Map(new AppInputSnapshot(true, AppButtons.Tab), AppScreen.MainMenu, 0, 0).Menu.VerticalStep);
            return count;
        }
        Assert.IsLessThanOrEqualTo(1, Math.Abs(Count(30) - Count(120)));
    }

    [TestMethod]
    public void FocusTransitionsClearAndGameplayRequireReleaseAndFreshNavigationPress()
    {
        var router = new InputRouter();
        var held = new AppInputSnapshot(true, AppButtons.Tab, AppButtons.Tab);
        Assert.AreEqual(1, router.Map(held, AppScreen.MainMenu, 0).Menu.VerticalStep);
        router.Map(held with { IsFocused = false }, AppScreen.MainMenu, 0, 1);
        Assert.AreEqual(0, router.Map(held, AppScreen.MainMenu, 0, 1).Menu.VerticalStep);
        Assert.AreEqual(0, router.Map(held with { Pressed = 0 }, AppScreen.MainMenu, 0, 1).Menu.VerticalStep);
        router.Map(new AppInputSnapshot(true), AppScreen.MainMenu, 0);
        Assert.AreEqual(1, router.Map(held, AppScreen.MainMenu, 0).Menu.VerticalStep);
        router.Clear(held); // Те же Clear вызываются Frames.SetScreen при входе в Settings/Back.
        Assert.AreEqual(0, router.Map(held, AppScreen.Settings, 0, 1).Menu.VerticalStep);
        Assert.AreEqual(0, router.Map(held with { Pressed = 0 }, AppScreen.Settings, 0, 1).Menu.VerticalStep);
        router.Map(new AppInputSnapshot(true), AppScreen.Settings, 0);
        Assert.AreEqual(1, router.Map(held, AppScreen.Settings, 0).Menu.VerticalStep);
        router.Map(held with { Pressed = 0 }, AppScreen.Playing, 0, 1);
        Assert.AreEqual(0, router.Map(held with { Pressed = 0 }, AppScreen.Settings, 0, 1).Menu.VerticalStep);
    }

    [TestMethod]
    public void OppositesCancelAndActivationBackRestartDoNotRepeat()
    {
        var router = new InputRouter();
        var opposite = AppButtons.Up | AppButtons.Down | AppButtons.Left | AppButtons.Right;
        Assert.AreEqual(default(MenuInput), router.Map(new AppInputSnapshot(true, opposite, opposite), AppScreen.MainMenu, 0).Menu);
        for (int i = 0; i < 10; i++)
            Assert.AreEqual(default(MenuInput), router.Map(new AppInputSnapshot(true, opposite), AppScreen.MainMenu, 0, 0.25).Menu);
        var actions = AppButtons.Enter | AppButtons.Space | AppButtons.Escape | AppButtons.R;
        var first = router.Map(new AppInputSnapshot(true, actions, actions), AppScreen.Paused, 0);
        Assert.IsTrue(first.Menu.ActivatePressed && first.Menu.BackPressed && first.RestartPressed);
        for (int i = 0; i < 10; i++)
        {
            var held = router.Map(new AppInputSnapshot(true, actions), AppScreen.Paused, 0, 0.25);
            Assert.IsFalse(held.Menu.ActivatePressed || held.Menu.BackPressed || held.RestartPressed);
        }
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => router.Map(new AppInputSnapshot(true), AppScreen.MainMenu, 0, double.NaN));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => router.Map(new AppInputSnapshot(true), AppScreen.MainMenu, 0, -1));
    }

    [TestMethod]
    public void HoldingLeftChangesActualMenuVolumeWhileKeepingFocusOnSlider()
    {
        var user = new UserSettings();
        var host = new GameplayHost(GameLevels.CreateCatalog(), SimulationSettings.Default, user);
        host.Flow.OpenSettings();
        host.Advance(new(true), 0);
        host.Advance(new(true, AppButtons.Left, AppButtons.Left), 0);
        Assert.AreEqual(0.95f, user.MasterVolume);
        for (int i = 0; i < 60; i++) host.Advance(new(true, AppButtons.Left), 1d / 60);
        Assert.IsLessThan(0.75f, user.MasterVolume);
        Assert.IsTrue(host.View.Controls.Single(control => control.Id == "volume").Selected);
        float stopped = user.MasterVolume;
        for (int i = 0; i < 60; i++) host.Advance(new(true), 1d / 60);
        Assert.AreEqual(stopped, user.MasterVolume);
        host.Advance(new(true, AppButtons.Right, AppButtons.Right), 0);
        for (int i = 0; i < 120; i++) host.Advance(new(true, AppButtons.Right), 1d / 60);
        Assert.AreEqual(1f, user.MasterVolume);
    }

    [TestMethod]
    public void EitherShiftWithTabNavigatesBackwards()
    {
        foreach (var shift in new[] { AppButtons.LeftShift, AppButtons.RightShift })
        {
            var router = new InputRouter();
            var input = new AppInputSnapshot(true, shift | AppButtons.Tab, AppButtons.Tab);
            Assert.AreEqual(-1, router.Map(input, AppScreen.MainMenu, 0).Menu.VerticalStep);
        }
        Assert.AreEqual(1, new InputRouter().Map(new AppInputSnapshot(true, AppButtons.Tab, AppButtons.Tab),
            AppScreen.MainMenu, 0).Menu.VerticalStep);
    }

    [TestMethod]
    public void HoldingNavigationRepeatsThroughTheRealFrameRunner()
    {
        var level = TrainingTrack.Create();
        var frames = new GameplayFrameRunner(SimulationSettings.Default);
        InputFrame Send(AppInputSnapshot input, double delta) =>
            frames.Advance(input, delta, Vector3.Zero, level, _ => Assert.Fail("Menu must not simulate gameplay.")).Input;
        Send(new(true), 0);
        int steps = Send(new(true, AppButtons.Down, AppButtons.Down), 0).Menu.VerticalStep;
        for (int i = 0; i < 60; i++) steps += Send(new(true, AppButtons.Down), 1d / 60).Menu.VerticalStep;
        Assert.IsTrue(steps >= 7 && steps <= 11, $"Expected controlled repeat, got {steps} steps in a second.");
    }
}
