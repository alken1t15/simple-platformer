using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Platformer.App;
using Platformer.Core;
using Engine3D.OpenGL;

namespace Platformer.Tests;

[TestClass]
public sealed class SettingsControlsTests
{
    [TestMethod]
    public void ControlsIsReachableByKeyboardAndEscapeReturnsOnePageAtATime()
    {
        var catalog = GameLevels.CreateCatalog();
        var host = new GameplayHost(catalog, SimulationSettings.Default, new());
        host.Flow.OpenSettings();
        host.Advance(new(true), 0);
        for (int i = 0; i < 8 && !host.View.Controls.Single(c => c.Id == "controls").Selected; i++)
        {
            host.Advance(new(true, AppButtons.Down, AppButtons.Down), 0);
            host.Advance(new(true), 0);
        }
        Assert.IsTrue(host.View.Controls.Single(c => c.Id == "controls").Selected);
        host.Advance(new(true, AppButtons.Enter, AppButtons.Enter), 0);
        Assert.AreEqual("Controls", host.View.Texts[0].Text);
        host.Advance(new(true), 0);
        host.Advance(new(true, AppButtons.Escape, AppButtons.Escape), 0);
        Assert.AreEqual("Settings", host.View.Texts[0].Text);
        Assert.IsTrue(host.View.Controls.Single(c => c.Id == "controls").Selected);
        host.Advance(new(true), 0);
        host.Advance(new(true, AppButtons.Escape, AppButtons.Escape), 0);
        Assert.AreEqual(AppScreen.MainMenu, host.Flow.Screen);
    }

    private static void Click(GameplayHost host, string id, float width = 1280, float height = 720)
    {
        host.Advance(new(true), 0, width, height);
        var b = host.View.Controls.Single(c => c.Id == id).Bounds;
        host.Advance(new(true, AppButtons.PointerLeft, AppButtons.PointerLeft,
            PointerPosition: new(b.X + b.Width / 2, b.Y + b.Height / 2)), 0, width, height);
        host.Advance(new(true), 0, width, height);
    }

    [TestMethod]
    public void ControlsCanBeReadFromSettingsAndBackRestoresFocusWithoutLeavingPause()
    {
        var catalog = new LevelCatalog([SessionFixtures.Level()]);
        var host = new GameplayHost(catalog, SimulationSettings.Default, new(), visual: true);
        Click(host, "play");
        host.Advance(new(true, AppButtons.Escape, AppButtons.Escape), 0);
        Click(host, "settings");
        var snapshot = host.Flow.Snapshot;
        Click(host, "controls");
        Assert.AreEqual(AppScreen.Settings, host.Flow.Screen);
        Assert.AreEqual("Controls", host.View.Texts[0].Text);
        foreach (var key in new[] { "WASD", "Mouse", "Space", "Shift", "R", "Esc", "Up / Down", "Left / Right", "Enter" })
            Assert.IsTrue(host.View.Texts.Any(t => t.Text.StartsWith(key, StringComparison.Ordinal)), key);
        Assert.HasCount(1, host.View.Controls);
        for (int i = 0; i < 10; i++) host.Advance(new(true), 0.1);
        Assert.AreSame(snapshot, host.Flow.Snapshot);
        host.Advance(new(true, AppButtons.Escape, AppButtons.Escape), 0);
        Assert.IsTrue(host.View.Controls.Single(c => c.Id == "controls").Selected);
        Click(host, "controls");
        Click(host, "back");
        Assert.IsTrue(host.View.Controls.Single(c => c.Id == "controls").Selected);
        Click(host, "back");
        Assert.AreEqual(AppScreen.Paused, host.Flow.Screen);
        Assert.AreSame(snapshot, host.Flow.Snapshot);
        Click(host, "continue");
        Assert.AreEqual(AppScreen.Playing, host.Flow.Screen);
        Assert.HasCount(3, host.View.Texts);
    }

    [TestMethod]
    public void SettingsRowsHavePaddingCenteredLabelsAndSeparateVolumeTrack()
    {
        var catalog = GameLevels.CreateCatalog();
        var host = new GameplayHost(catalog, SimulationSettings.Default, new(), visual: true);
        host.Flow.OpenSettings();
        foreach (var (width, height) in new[] { (800f, 450f), (1280f, 720f), (1920f, 1080f) })
        {
            host.RefreshView(width, height);
            float scale = width / 1280;
            Assert.HasCount(8, host.View.Controls);
            for (int i = 0; i < host.View.Controls.Count; i++)
            {
                var control = host.View.Controls[i];
                var b = control.Bounds;
                Assert.AreEqual(44 * scale, b.Height);
                var text = host.View.Texts.Single(t => t.Text == control.Label);
                var ink = OverlayTextMetrics.MeasureInkBounds(text.Text, text.PixelHeight);
                Assert.IsTrue(text.Position.Y > b.Y && text.Position.Y + text.PixelHeight < b.Y + b.Height);
                if (control.Kind == MenuControlKind.Volume)
                    Assert.IsGreaterThan(text.Position.Y + text.PixelHeight, control.ValueBounds!.Value.Y);
                Assert.AreEqual(b.Y + b.Height / 2, text.Position.Y + ink.Y + ink.Height / 2, 1e-4f);
                if (i > 0) Assert.IsGreaterThan(host.View.Controls[i - 1].Bounds.Y + b.Height, b.Y);
                Assert.IsLessThanOrEqualTo(height, b.Y + b.Height);
            }
        }
    }

    [TestMethod]
    public void MainMenuGeometryIsUnchangedAndGameDoesNotShowHelpPanel()
    {
        var catalog = GameLevels.CreateCatalog();
        var host = new GameplayHost(catalog, SimulationSettings.Default, new(), visual: true);
        host.RefreshView(1280, 720);
        string[] ids = ["play", "levels", "settings", "skins", "exit"];
        for (int i = 0; i < ids.Length; i++)
            Assert.AreEqual(new MenuRect(104, 305 + i * 48, 360, 40), host.View.Controls.Single(c => c.Id == ids[i]).Bounds);
        Assert.IsFalse(host.View.Texts.Any(t => t.Text.Contains("choose", StringComparison.Ordinal)));
        foreach (var level in catalog.Levels.Take(4))
        {
            var flow = new LevelFlow(catalog, SimulationSettings.Default, new());
            flow.Start(level.Id);
            var view = new MenuController(flow, catalog).BuildView(1280, 720);
            Assert.HasCount(3, view.Texts);
            Assert.HasCount(1, view.Panels);
        }
    }
}
