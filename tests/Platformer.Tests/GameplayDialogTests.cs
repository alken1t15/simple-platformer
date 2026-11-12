using System.Numerics;
using Engine3D.OpenGL;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Platformer.App;
using Platformer.Core;

namespace Platformer.Tests;

[TestClass]
public sealed class GameplayDialogTests
{
    private static LevelDefinition Route(bool falling) => new("dialog-test", new(0, 0.501f, 0), -3,
        new(new(falling ? 50 : 0.2f, 0.75f, 0), new(0.1f, 0.75f, 1), Quaternion.Identity),
        falling ? [] : [new("floor", new(new(0, -0.2f, 0), new(10, 0.2f, 3), Quaternion.Identity))],
        [new("far-one", new(7, 0.8f, 0), 0.2f), new("far-two", new(8, 0.8f, 0), 0.2f)], []);

    private static (LevelFlow Flow, MenuController Menu) Create(bool falling, bool record, bool visual)
    {
        var level = Route(falling);
        var catalog = new LevelCatalog([level]);
        var progress = new PlayerProgress(catalog);
        if (record) progress.RecordCompletion(level.Id, level.Coins.Count);
        var flow = new LevelFlow(catalog, SimulationSettings.Default, new(), progress: progress, visual: visual);
        flow.Start(level.Id);
        return (flow, new(flow, catalog, visual: visual));
    }

    private static void Click(MenuController menu, string id, float width = 800, float height = 450)
    {
        var bounds = menu.BuildView(width, height).Controls.Single(c => c.Id == id).Bounds;
        menu.Handle(new(default, false, false, new(0, 0, false, false, true,
            new(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2))), width, height);
    }

    private static int[] FilledStars(MenuView view) => view.Images.Where(image => image.Texture.Width == 32).Select(image =>
        image.Texture.Pixels[((image.Texture.Height / 2) * image.Texture.Width + image.Texture.Width / 2) * 4 + 3] == 255 ? 1 : 0).ToArray();

    [TestMethod]
    [DataRow(false)] [DataRow(true)]
    public void FailureShowsBestSuccessfulFinishAndRetryDoesNotLoseIt(bool visual)
    {
        var (flow, menu) = Create(falling: true, record: true, visual);
        SessionFixtures.Until(() => flow.Screen == AppScreen.Failed, () => flow.Step(default));
        var view = menu.BuildView(800, 450);
        CollectionAssert.AreEqual(new[] { 1, 1, 1 }, FilledStars(view));
        Assert.IsTrue(view.Texts.Any(t => t.Text == "PERSONAL BEST"));
        Assert.IsFalse(view.Texts.Any(t => t.Text.Contains('[') || t.Text.Contains("Coins", StringComparison.Ordinal)));
        Assert.AreEqual(3, flow.Progress.StarsFor("dialog-test"));
        Click(menu, "retry");
        Assert.AreEqual(AppScreen.Playing, flow.Screen);
        Assert.AreEqual(0L, flow.Snapshot!.Tick);
        Assert.AreEqual(3, flow.Progress.StarsFor("dialog-test"));
    }

    [TestMethod]
    public void FirstFailureHasNoAwardedStarsAndExplainsHowToEarnThem()
    {
        var (flow, menu) = Create(falling: true, record: false, visual: true);
        SessionFixtures.Until(() => flow.Screen == AppScreen.Failed, () => flow.Step(default));
        var view = menu.BuildView(800, 450);
        CollectionAssert.AreEqual(new[] { 0, 0, 0 }, FilledStars(view));
        Assert.IsTrue(view.Texts.Any(t => t.Text == "Finish this level to earn your first stars."));
        Assert.AreEqual(0, flow.Progress.TotalStars);
    }

    [TestMethod]
    [DataRow(false)] [DataRow(true)]
    public void VictorySeparatesCurrentFinishFromBetterSavedRecordWithoutAsciiOrCoinCounts(bool visual)
    {
        var (flow, menu) = Create(falling: false, record: true, visual);
        flow.Step(default);
        Assert.AreEqual(AppScreen.LevelResult, flow.Screen);
        var view = menu.BuildView(800, 450);
        CollectionAssert.AreEqual(new[] { 1, 0, 0, 1, 1, 1 }, FilledStars(view));
        Assert.IsTrue(view.Texts.Any(t => t.Text == "THIS FINISH"));
        Assert.IsTrue(view.Texts.Any(t => t.Text == "PERSONAL BEST"));
        Assert.IsFalse(view.Texts.Any(t => t.Text.Contains('[') || t.Text.Contains("Coins", StringComparison.Ordinal)
            || t.Text.Contains("Rating:", StringComparison.Ordinal) || t.Text.Contains("Best:", StringComparison.Ordinal)));
        Assert.AreEqual(3, flow.Progress.StarsFor("dialog-test"));
        Click(menu, "next");
        Assert.AreEqual(AppScreen.GameComplete, flow.Screen);
        Assert.IsFalse(menu.BuildView(800, 450).Texts.Any(t => t.Text.Contains("coins", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public void PauseButtonsAndMainMenuConfirmationRetainTheSameAttempt()
    {
        var (flow, menu) = Create(falling: false, record: false, visual: true);
        flow.Pause();
        var before = flow.Snapshot!;
        var view = menu.BuildView(800, 450);
        CollectionAssert.AreEqual(new[] { "continue", "restart", "settings", "main-menu" }, view.Controls.Select(c => c.Id).ToArray());
        Click(menu, "settings");
        Assert.AreEqual(AppScreen.Settings, flow.Screen);
        Click(menu, "back");
        Assert.AreEqual(AppScreen.Paused, flow.Screen);
        Click(menu, "main-menu");
        view = menu.BuildView(800, 450);
        Assert.HasCount(2, view.Controls);
        Assert.IsEmpty(view.Images);
        Assert.AreEqual("cancel-main-menu", view.Controls.Single(c => c.Selected).Id);
        Click(menu, "cancel-main-menu");
        Assert.AreSame(before, flow.Snapshot);
        Click(menu, "continue");
        Assert.AreEqual(AppScreen.Playing, flow.Screen);
        Assert.AreSame(before, flow.Snapshot);
    }

    [TestMethod]
    [DataRow(false)] [DataRow(true)]
    public void DialogsFitAtMinimumAndFullHdAndUseMatchingDrawingAndHitBounds(bool visual)
    {
        foreach (var screen in new[] { AppScreen.Paused, AppScreen.Failed, AppScreen.LevelResult })
        {
            var (flow, menu) = Create(falling: screen == AppScreen.Failed, record: true, visual);
            if (screen == AppScreen.Paused) flow.Pause();
            else SessionFixtures.Until(() => flow.Screen == screen, () => flow.Step(default));
            foreach (var (width, height) in new[] { (800f, 450f), (1280f, 720f), (1920f, 1080f) })
            {
                var view = menu.BuildView(width, height);
                var overlay = MenuOverlayAdapter.Build(view, visual);
                Assert.AreEqual(view.Texts.Count, overlay.Texts.Length);
                foreach (var control in view.Controls)
                {
                    var b = control.Bounds;
                    Assert.IsGreaterThanOrEqualTo(0f, b.X); Assert.IsGreaterThanOrEqualTo(0f, b.Y);
                    Assert.IsLessThanOrEqualTo(width, b.X + b.Width); Assert.IsLessThanOrEqualTo(height, b.Y + b.Height);
                    Assert.IsTrue(view.Panels.Any(p => p.Kind == MenuPanelKind.Control && p.Bounds == b));
                    Assert.AreEqual(1, view.Controls.Count(c => c.Bounds.Contains(new(b.X + b.Width / 2, b.Y + b.Height / 2))));
                    var text = view.Texts.Single(t => t.Text == control.Label);
                    var ink = OverlayTextMetrics.MeasureInkBounds(text.Text, text.PixelHeight);
                    Assert.AreEqual(b.Y + b.Height / 2, text.Position.Y + ink.Y + ink.Height / 2, 0.001f);
                }
            }
        }
    }

    [TestMethod]
    public void SkyExplanationCountsMasteredBaseLevelsAndDisappearsAfterUnlock()
    {
        var catalog = GameLevels.CreateCatalog();
        var progress = new PlayerProgress(catalog);
        var flow = new LevelFlow(catalog, SimulationSettings.Default, new(), progress: progress);
        var menu = new MenuController(flow, catalog);
        flow.OpenLevelSelect();
        foreach (var level in catalog.Levels.Take(4)) progress.RecordCompletion(level.Id, level.Coins.Count / 2);
        var view = menu.BuildView(800, 450);
        Assert.IsTrue(view.Texts.Any(t => t.Text == "Levels mastered: 0/4"));
        Assert.IsFalse(view.Texts.Any(t => t.Text.Contains("/12", StringComparison.Ordinal)));
        Assert.IsFalse(view.Controls.Single(c => c.Id == "level:sky-storm").Enabled);
        foreach (var level in catalog.Levels.Take(4)) progress.RecordCompletion(level.Id, level.Coins.Count);
        view = menu.BuildView(800, 450);
        Assert.IsTrue(view.Controls.Single(c => c.Id == "level:sky-storm").Enabled);
        Assert.IsFalse(view.Texts.Any(t => t.Text.StartsWith("Sky Storm", StringComparison.Ordinal)
            || t.Text.StartsWith("Levels mastered", StringComparison.Ordinal)));
    }
}
