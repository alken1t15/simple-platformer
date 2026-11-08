using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Platformer.App;
using Platformer.Core;

namespace Platformer.Tests;

[TestClass]
public sealed class MenuLayoutTests
{
    [TestMethod]
    public void AllNineScreensHaveAsciiContentAndMatchingPanelControlBoundsAtDifferentSizes()
    {
        var first = SessionFixtures.Level(finishX: 0f);
        var second = SessionFixtures.Level("level2", finishX: 0f);
        var catalog = new LevelCatalog([first, second]);
        var flow = new LevelFlow(catalog, SimulationSettings.Default, new());
        var menu = new MenuController(flow, catalog);
        var screens = new HashSet<AppScreen>();
        void Check()
        {
            screens.Add(flow.Screen);
            foreach (var (width, height) in new[] { (1280f, 720f), (640f, 480f), (320f, 200f), (1920f, 400f), (400f, 1200f) })
            {
                var view = menu.BuildView(width, height);
                Assert.IsNotEmpty(view.Texts);
                foreach (var text in view.Texts)
                {
                    Assert.IsTrue(text.Text.All(character => character is >= ' ' and <= '~'), text.Text);
                    Assert.IsTrue(float.IsFinite(text.PixelHeight) && text.PixelHeight > 0f);
                    Assert.IsTrue(text.Position.X >= 0f && text.Position.X < width);
                    Assert.IsTrue(text.Position.Y >= 0f && text.Position.Y < height);
                }
                foreach (var panel in view.Panels)
                {
                    var b = panel.Bounds;
                    Assert.IsTrue(b.X >= 0f && b.Y >= 0f && b.Width >= 0f && b.Height >= 0f);
                    Assert.IsTrue(b.X + b.Width <= width + 0.001f && b.Y + b.Height <= height + 0.001f);
                }
                foreach (var control in view.Controls)
                {
                    Assert.IsTrue(view.Panels.Any(panel => panel.Bounds == control.Bounds));
                    string label = control.Kind == MenuControlKind.Skin
                        ? GameSkins.Get(control.Id["skin:".Length..]).DisplayName : control.Label;
                    Assert.IsTrue(view.Texts.Any(text => text.Text == label && control.Bounds.Contains(text.Position)));
                    if (control.PreviewBounds is { } preview)
                        Assert.IsTrue(preview.X >= control.Bounds.X && preview.Y >= control.Bounds.Y
                            && preview.X + preview.Width <= control.Bounds.X + control.Bounds.Width + 0.001f
                            && preview.Y + preview.Height <= control.Bounds.Y + control.Bounds.Height + 0.001f);
                    Assert.AreEqual(1, view.Controls.Count(other => other.Bounds.Contains(
                        new(control.Bounds.X + control.Bounds.Width / 2f, control.Bounds.Y + control.Bounds.Height / 2f))));
                }
            }
        }
        Check();
        flow.OpenLevelSelect(); Check(); flow.Back();
        flow.OpenSettings(); Check(); flow.Back();
        flow.OpenSkins(); Check(); flow.Back();
        flow.Start(first.Id); Check();
        flow.Pause(); Check(); flow.Continue();
        flow.Step(default); Check(); flow.Next();
        flow.Step(default); flow.Next(); Check();
        var failedLevel = SessionFixtures.Level("falling", finishX: 40f, floor: false);
        var failedCatalog = new LevelCatalog([failedLevel]);
        flow = new(failedCatalog, SimulationSettings.Default, new());
        menu = new(flow, failedCatalog);
        flow.Start(failedLevel.Id);
        SessionFixtures.Until(() => flow.Screen == AppScreen.Failed, () => flow.Step(default));
        Check();
        CollectionAssert.AreEquivalent(Enum.GetValues<AppScreen>(), screens.ToArray());
    }

    [TestMethod]
    public void ClickAfterResizeUsesNewLogicalGeometryAndOutsideClicksDoNothing()
    {
        var catalog = new LevelCatalog([SessionFixtures.Level()]);
        var flow = new LevelFlow(catalog, SimulationSettings.Default, new());
        var menu = new MenuController(flow, catalog);
        var old = menu.BuildView(1280f, 720f).Controls.Single(control => control.Id == "settings").Bounds;
        var current = menu.BuildView(640f, 480f).Controls.Single(control => control.Id == "settings").Bounds;
        Assert.AreNotEqual(old, current);
        menu.Handle(new(default, false, false, new(0, 0, false, false, true, new(old.X + 2f, old.Y + 2f))), 640f, 480f);
        Assert.AreEqual(AppScreen.MainMenu, flow.Screen);
        menu.Handle(new(default, false, false, new(0, 0, false, false, true,
            new(current.X + current.Width / 2f, current.Y + current.Height / 2f))), 640f, 480f);
        Assert.AreEqual(AppScreen.Settings, flow.Screen);
    }

    [TestMethod]
    public void HitTestUsesExclusiveBottomAndRightEdges()
    {
        var rect = new MenuRect(10f, 20f, 40f, 30f);
        Assert.IsTrue(rect.Contains(new(10f, 20f)));
        Assert.IsTrue(rect.Contains(new(49.99f, 49.99f)));
        Assert.IsFalse(rect.Contains(new(50f, 30f)));
        Assert.IsFalse(rect.Contains(new(20f, 50f)));
        Assert.IsFalse(rect.Contains(new(float.NaN, 30f)));
        Assert.IsFalse(new MenuRect(0f, 0f, 0f, 0f).Contains(Vector2.Zero));
    }

    [TestMethod]
    public void SliderValueUsesTheDrawnTrackAtEachLogicalSize()
    {
        var catalog = new LevelCatalog([SessionFixtures.Level()]);
        var settings = new UserSettings();
        var flow = new LevelFlow(catalog, SimulationSettings.Default, settings);
        var menu = new MenuController(flow, catalog);
        flow.OpenSettings();
        foreach (var (width, height) in new[] { (1280f, 720f), (640f, 480f), (320f, 200f) })
        {
            var view = menu.BuildView(width, height);
            var track = view.Controls.Single(control => control.Id == "volume").ValueBounds!.Value;
            foreach (float fraction in new[] { 0f, 0.25f, 0.75f, 1f })
            {
                menu.Handle(new(default, false, false, new(0, 0, false, false, true,
                    new(track.X + track.Width * fraction, track.Y))), width, height);
                Assert.AreEqual(fraction, settings.MasterVolume, 1e-6f);
                Assert.IsTrue(menu.BuildView(width, height).Panels.Any(panel =>
                    panel.Bounds == track with { Width = track.Width * fraction }));
            }
        }
    }
}
