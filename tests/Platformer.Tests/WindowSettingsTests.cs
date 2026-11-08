using Microsoft.VisualStudio.TestTools.UnitTesting;
using Platformer.App;
using Platformer.Core;

namespace Platformer.Tests;

[TestClass]
public sealed class WindowSettingsTests
{
    [TestMethod]
    public void FullHdIsAvailableInFullscreenEvenWhenWindowWorkAreaIsSmaller()
    {
        var settings = new WindowSettings();
        settings.Synchronize(960, 640, 800, 450, 1904, 993, false, 1920, 1080);
        Assert.IsFalse(settings.Choices.Contains(new(1920, 1080)));
        settings.ToggleMode();
        Assert.IsTrue(settings.SelectedFullscreen);
        CollectionAssert.AreEqual(new[] { new WindowResolution(1920, 1080) }, settings.Choices.ToArray());
        Assert.AreEqual(new(1920, 1080), settings.Selected);
        Assert.IsFalse(settings.ActualFullscreen);
        Assert.IsNull(settings.TakeDisplayRequest());
        settings.RequestApply();
        Assert.AreEqual(new WindowDisplayRequest(new(1920, 1080), true), settings.TakeDisplayRequest());
        Assert.IsNull(settings.TakeDisplayRequest());
        Assert.AreEqual(new(960, 640), settings.Actual);
        settings.Synchronize(1920, 1080, 800, 450, 1904, 993, true, 1920, 1080);
        Assert.IsTrue(settings.ActualFullscreen);
        settings.ToggleMode();
        Assert.AreEqual(new(960, 640), settings.Selected);
        Assert.IsFalse(settings.SelectedFullscreen);
        Assert.IsFalse(settings.Choices.Contains(new(1920, 1080)));
        settings.RequestApply();
        Assert.AreEqual(new WindowDisplayRequest(new(960, 640), false), settings.TakeDisplayRequest());
    }

    [TestMethod]
    public void ReopeningSettingsCancelsUnappliedModeAndMonitorChangeInvalidatesRequest()
    {
        var settings = new WindowSettings();
        settings.Synchronize(1024, 576, 800, 450, 1904, 993, false, 1920, 1080);
        settings.SelectActual();
        settings.ToggleMode();
        settings.SelectActual();
        Assert.IsFalse(settings.SelectedFullscreen);
        Assert.AreEqual(new(1024, 576), settings.Selected);
        settings.ToggleMode();
        settings.RequestApply();
        settings.Synchronize(1024, 576, 800, 450, 1264, 713, false, 1280, 800);
        Assert.IsNull(settings.TakeDisplayRequest());
        Assert.AreEqual(new(1280, 800), settings.Selected);
        settings.Synchronize(1280, 800, 800, 450, 1264, 713, true, 1280, 800);
        settings.SelectActual();
        Assert.IsTrue(settings.SelectedFullscreen);
        settings.Synchronize(0, 0, 800, 450, 1264, 713, true, 1280, 800);
        Assert.AreEqual(new(1280, 800), settings.Actual);
    }

    [TestMethod]
    public void ChoicesExcludeSizesOutsideMonitorAndIncludeActualManualSize()
    {
        var settings = new WindowSettings();
        settings.Synchronize(1100, 650, 800, 450, 1904, 1009);
        Assert.IsTrue(settings.Choices.Contains(new(1100, 650)));
        Assert.IsFalse(settings.Choices.Contains(new(1920, 1080)));
        Assert.IsTrue(settings.Choices.Contains(new(1600, 900)));
        Assert.IsTrue(settings.Choices.All(size => size.Width >= 800 && size.Width <= 1904
            && size.Height >= 450 && size.Height <= 1009));
        settings.SelectActual();
        Assert.AreEqual(new(1100, 650), settings.Selected);
        for (int i = 0; i < settings.Choices.Count; i++) settings.Cycle(1);
        Assert.AreEqual(new(1100, 650), settings.Selected);
        settings.Cycle(-1);
        settings.Cycle(1);
        Assert.AreEqual(new(1100, 650), settings.Selected);
    }

    [TestMethod]
    public void ApplyIsOneShotAndActualWaitsForOperatingSystemAcknowledgement()
    {
        var settings = new WindowSettings();
        settings.Synchronize(960, 640, 800, 450, 1904, 1009);
        settings.Cycle(1);
        Assert.AreEqual(new(1024, 576), settings.Selected);
        Assert.IsNull(settings.TakeRequest());
        settings.RequestApply();
        Assert.AreEqual(new(1024, 576), settings.TakeRequest());
        Assert.IsNull(settings.TakeRequest());
        Assert.AreEqual(new(960, 640), settings.Actual);
        settings.Synchronize(1016, 570, 800, 450, 1904, 1009); // ОС скорректировала размер.
        Assert.AreEqual(new(1016, 570), settings.Actual);
        settings.Synchronize(0, 0, 800, 450, 1904, 1009);
        Assert.AreEqual(new(1016, 570), settings.Actual);
    }

    [TestMethod]
    public void MonitorChangeInvalidatesPendingSizeAndSmallDisplayHasUsableFallback()
    {
        var settings = new WindowSettings();
        settings.Synchronize(960, 640, 800, 450, 1904, 1009);
        settings.Cycle(1);
        settings.RequestApply();
        settings.Synchronize(640, 400, 640, 400, 640, 400);
        Assert.IsNull(settings.TakeRequest());
        CollectionAssert.AreEqual(new[] { new WindowResolution(640, 400) }, settings.Choices.ToArray());
        settings.Cycle(1);
        settings.Cycle(-1);
        settings.RequestApply();
        Assert.AreEqual(new(640, 400), settings.TakeRequest());
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => settings.Synchronize(1, 1, 800, 450, 640, 400));
    }

    [TestMethod]
    public void MenuChangesSizeOnlyOnApplyAndKeepsPausedSessionAndVolume()
    {
        var catalog = GameLevels.CreateCatalog();
        var user = new UserSettings { MasterVolume = 0.35f, SkinId = "orange" };
        var flow = new LevelFlow(catalog, SimulationSettings.Default, user, () => { });
        var menu = new MenuController(flow, catalog);
        menu.Window.Synchronize(960, 640, 800, 450, 1904, 1009, false, 1920, 1080);
        flow.Start(catalog.Levels[0].Id);
        flow.Pause();
        var session = flow.Snapshot!.SessionId;
        flow.OpenSettings();
        menu.BuildView(960, 640);
        void Click(string id)
        {
            var bounds = menu.BuildView(960, 640).Controls.Single(control => control.Id == id).Bounds;
            menu.Handle(new(default, false, false, new(0, 0, false, false, true,
                new(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2))), 960, 640);
        }
        Click("resolution");
        Assert.AreEqual(new(1024, 576), menu.Window.Selected);
        Assert.IsNull(menu.Window.TakeRequest());
        menu.Handle(new(default, false, false, new(-1, 0, false, false, false, default)), 960, 640);
        Assert.AreEqual(new(960, 640), menu.Window.Selected);
        Click("resolution");
        Click("apply-resolution");
        Assert.AreEqual(new(1024, 576), menu.Window.TakeRequest());
        Assert.IsTrue(menu.BuildView(800, 450).Texts.Any(text => text.Text == "Current window: 960 x 640"));
        Assert.AreEqual(AppScreen.Settings, flow.Screen);
        Assert.AreEqual(session, flow.Snapshot.SessionId);
        Assert.AreEqual(0.35f, user.MasterVolume);
        Assert.AreEqual("orange", user.SkinId);
        Click("display-mode");
        Assert.IsTrue(menu.Window.SelectedFullscreen);
        Assert.IsFalse(menu.Window.ActualFullscreen);
        Assert.IsTrue(menu.BuildView(800, 450).Controls.Any(control => control.Label == "Resolution: < 1920 x 1080 >"));
        Click("apply-resolution");
        Assert.AreEqual(new WindowDisplayRequest(new(1920, 1080), true), menu.Window.TakeDisplayRequest());
        Assert.AreEqual(session, flow.Snapshot.SessionId);
        Click("back");
        Assert.AreEqual(AppScreen.Paused, flow.Screen);
    }
}
