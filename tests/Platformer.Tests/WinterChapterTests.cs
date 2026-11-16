using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Platformer.App;
using Platformer.Core;

namespace Platformer.Tests;

[TestClass]
public sealed class WinterChapterTests
{
    internal static readonly string[] Ids = ["winter-snowbound", "winter-icicle", "winter-lake", "winter-aurora"];

    internal static void Run(ProductionRouteTests.Driver d, string id)
    {
        void Go(float x, float z) => d.Go(x, z);
        void Cross(string hazardId, float x, float z)
        {
            Go(x - 2.5f, z);
            var hazard = GameLevels.CreateCatalog().Get(id).Hazards.Single(h => h.Id == hazardId);
            bool ready = false;
            for (int i = 0; i < 1500; i++)
            {
                double time = d.Host.Flow.Snapshot!.SimulationSeconds;
                bool safe = new[] { 0.4, 0.65, 0.9, 1.1 }.All(offset =>
                {
                    var box = HazardMotion.Evaluate(hazard, time + offset).Bounds;
                    return hazard.Response == HazardResponse.Fail ? box.Center.Y - box.HalfExtents.Y > d.Host.Flow.Snapshot.Ball.Center.Y + 0.65f
                        : MathF.Abs(box.Center.Z - z) > 2.1f;
                });
                if (safe) { ready = true; break; }
                var ball = d.Host.Flow.Snapshot.Ball;
                d.Move(new Vector2(x - 2.5f - ball.Center.X, z - ball.Center.Z) * 5 - new Vector2(ball.Velocity.X, ball.Velocity.Z));
            }
            Assert.IsTrue(ready, hazardId);
            Go(x + 2, z);
        }
        void Hop(float edge, Vector2 axis, float x, float z)
        {
            var ball = d.Host.Flow.Snapshot!.Ball;
            Vector2 p = new(ball.Center.X, ball.Center.Z);
            var approach = p + axis * (edge - Vector2.Dot(p, axis) - 0.8f);
            Go(approach.X, approach.Y);
            for (int i = 0; i < 180 && Vector2.Dot(new(d.Host.Flow.Snapshot!.Ball.Center.X, d.Host.Flow.Snapshot.Ball.Center.Z), axis) < edge - 0.3f; i++) d.Move(axis);
            Assert.IsTrue(d.Host.Flow.Snapshot!.Ball.Grounded, id + " takeoff");
            d.Move(axis, jump: true); Go(x, z);
            for (int i = 0; i < 90 && !d.Host.Flow.Snapshot!.Ball.Grounded; i++)
            {
                ball = d.Host.Flow.Snapshot.Ball;
                d.Move(new Vector2(x - ball.Center.X, z - ball.Center.Z) * 5 - new Vector2(ball.Velocity.X, ball.Velocity.Z));
            }
            Assert.IsTrue(d.Host.Flow.Snapshot!.Ball.Grounded, id + " landing");
        }
        switch (id)
        {
            case "winter-snowbound":
                Go(0, 0); Go(10, 0); Cross("snowball-pass", 16, 0); Go(22, 0); Go(22, 4); Go(22, 8);
                Hop(24, Vector2.UnitX, 27, 8); Go(29, 8); break;
            case "winter-icicle":
                Go(0, 0); Go(6, 0); Cross("ice-press-one", 9, 0); Go(14, 0); Cross("ice-press-two", 17, 0);
                Go(22, 0); Go(22, 4); Go(22, 8); Go(26.5f, 8); Go(31, 8); break;
            case "winter-lake":
                Go(0, 0); Hop(2, Vector2.UnitX, 5, 0); Hop(6.5f, Vector2.UnitX, 9.5f, 0);
                Hop(11, Vector2.UnitX, 14, 0); Hop(15.5f, Vector2.UnitX, 18.7f, 0);
                Go(25, 0); Go(26, 0); Go(18.7f, 0); Go(18.7f, 7.5f);
                Hop(9, Vector2.UnitY, 18.7f, 11.8f); Hop(13.2f, Vector2.UnitY, 18.7f, 16);
                Hop(17.4f, Vector2.UnitY, 18.7f, 20.4f); Go(16.7f, 20.4f); Go(11, 18.5f); Go(10, 18.5f); break;
            case "winter-aurora":
                Go(0, 0); Go(10, 0); Cross("snowball-summit", 16, 0); Go(22, 0); Go(22, 5); Go(22, 10);
                Go(26, 10); Cross("ice-press-summit", 29, 10); Go(35.5f, 10);
                Hop(37, Vector2.UnitX, 40, 10); Hop(41.5f, Vector2.UnitX, 44.5f, 10); Go(45.2f, 10); break;
        }
    }

    [TestMethod]
    [DataRow("winter-snowbound", false)] [DataRow("winter-snowbound", true)]
    [DataRow("winter-icicle", false)] [DataRow("winter-icicle", true)]
    [DataRow("winter-lake", false)] [DataRow("winter-lake", true)]
    [DataRow("winter-aurora", false)] [DataRow("winter-aurora", true)]
    public void EachWinterRoutePassesThroughActualInputWithAllCoinsAndRestart(string id, bool visual)
    {
        var host = new GameplayHost(GameLevels.CreateCatalog(), SimulationSettings.Default, new(), visual: visual);
        host.Flow.Start(id);
        var driver = new ProductionRouteTests.Driver(host);
        driver.Press(AppButtons.None); Run(driver, id);
        Assert.AreEqual(AppScreen.LevelResult, host.Flow.Screen);
        Assert.AreEqual(12, host.Flow.Snapshot!.CoinCount, string.Join(",", GameLevels.CreateCatalog().Get(id).Coins.Select(c => c.Id).Except(host.Flow.Snapshot.CollectedCoinIds)));
        Assert.AreEqual(3, host.Flow.Progress.StarsFor(id));
        Assert.AreEqual(0, host.Flow.Progress.BaseStars);
        host.Flow.Restart();
        Assert.AreEqual(0, host.Flow.Snapshot!.CoinCount);
        Assert.AreEqual(0d, host.Flow.Snapshot.SimulationSeconds);
        Assert.IsTrue(host.Flow.Snapshot.Platforms.All(p => !p.Collapsed));
    }

    [TestMethod]
    [DataRow(false)] [DataRow(true)]
    public void WinterTourIsIndependentAndCompletesItsFourLocations(bool visual)
    {
        var host = new GameplayHost(new LevelCatalog(GameLevels.CreateCatalog().Levels.Take(17).ToArray()), SimulationSettings.Default, new(), visual: visual);
        host.Flow.Start(Ids[0]);
        var driver = new ProductionRouteTests.Driver(host); driver.Press(AppButtons.None);
        foreach (string id in Ids)
        {
            Assert.AreEqual(id, host.Flow.Snapshot!.LevelId); Run(driver, id);
            Assert.AreEqual(12, host.Flow.Snapshot!.CoinCount); driver.Press(AppButtons.Enter);
        }
        Assert.AreEqual(AppScreen.GameComplete, host.Flow.Screen);
        Assert.AreEqual(48, host.Flow.TotalCoinCount);
        Assert.AreEqual(12, host.Flow.Progress.TotalStars);
        Assert.AreEqual(0, host.Flow.Progress.BaseStars);
        Assert.IsFalse(host.Flow.Progress.IsLevelUnlocked(GameLevels.FinalLevelId));
        Assert.IsTrue(host.Menu.BuildView(800, 450).Texts.Any(t => t.Text == "Winter chapter complete!"));
    }

    [TestMethod]
    public void PreviousTropicalSaveKeepsAllRecordsAndWinterStarsDoNotUnlockSky()
    {
        var catalog = GameLevels.CreateCatalog();
        var oldCatalog = new LevelCatalog(catalog.Levels.Take(13).ToArray());
        var oldProgress = new PlayerProgress(oldCatalog);
        foreach (var level in oldCatalog.Levels) oldProgress.RecordCompletion(level.Id, level.Coins.Count);
        string directory = Path.Combine(Path.GetTempPath(), "ball-winter-save-" + Guid.NewGuid().ToString("N"));
        string path = Path.Combine(directory, "progress.json");
        try
        {
            Assert.IsTrue(new ProgressStore(path).Save(oldProgress));
            var loaded = new ProgressStore(path).Load(catalog);
            CollectionAssert.AreEquivalent(oldProgress.BestCoins.ToArray(), loaded.BestCoins.ToArray());
            Assert.AreEqual(39, loaded.TotalStars); Assert.AreEqual(60, loaded.MaximumStars);
            Assert.IsTrue(loaded.IsLevelUnlocked(GameLevels.FinalLevelId));
            var fresh = new PlayerProgress(catalog);
            foreach (string id in Ids) fresh.RecordCompletion(id, 12);
            Assert.AreEqual(12, fresh.TotalStars); Assert.AreEqual(0, fresh.BaseStars);
            Assert.IsFalse(fresh.IsLevelUnlocked(GameLevels.FinalLevelId));
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
            if (Directory.Exists(directory)) Directory.Delete(directory);
        }
    }

    [TestMethod]
    [DataRow(false)] [DataRow(true)]
    public void ChapterSwitchKeepsProgressAndAllCardsAndFooterInsideTheWindow(bool visual)
    {
        foreach (var (width, height) in new[] { (800f, 450f), (1920f, 1080f) })
        {
            var catalog = GameLevels.CreateCatalog();
            var flow = new LevelFlow(catalog, SimulationSettings.Default, new());
            var menu = new MenuController(flow, catalog, visual: visual);
            flow.OpenLevelSelect();
            for (int cycle = 0; cycle < 4; cycle++)
            {
                var view = menu.BuildView(width, height);
                var chapter = cycle % 2 == 0 ? GameChapter.Tropical : GameChapter.Winter;
                Assert.IsTrue(view.Texts.Any(t => t.Text == GameChapters.Name(chapter)));
                Assert.HasCount(chapter == GameChapter.Winter ? 7 : 13, view.Controls.Where(c => c.Kind == MenuControlKind.Level).ToArray());
                foreach (var control in view.Controls)
                {
                    var b = control.Bounds;
                    Assert.IsLessThanOrEqualTo(width, b.X + b.Width); Assert.IsLessThanOrEqualTo(height, b.Y + b.Height);
                    Assert.AreEqual(1, view.Controls.Count(c => c.Bounds.Contains(new(b.X + b.Width / 2, b.Y + b.Height / 2))));
                }
                var footer = view.Controls.Single(c => c.Id == "chapter").Bounds;
                menu.Handle(new(default, false, false, new(0, 0, false, false, true,
                    new(footer.X + footer.Width / 2, footer.Y + footer.Height / 2))), width, height);
                Assert.IsNull(flow.Snapshot); Assert.AreEqual(0, flow.Progress.TotalStars);
            }
        }
    }

    [TestMethod]
    public void WinterLightingAndHazardBoundsFollowPresenterAndResetAcrossChapters()
    {
        var catalog = GameLevels.CreateCatalog();
        var host = new GameplayHost(catalog, SimulationSettings.Default, new(), visual: true);
        var tropicalLight = host.Scene.Lighting.AmbientColor;
        host.Flow.Start("winter-icicle"); host.Advance(new(true), 0);
        Assert.AreNotEqual(tropicalLight, host.Scene.Lighting.AmbientColor);
        Assert.AreEqual(host.Flow.Presenter!.Scene.Lighting.AmbientColor, host.Scene.Lighting.AmbientColor);
        foreach (var pose in host.Flow.Snapshot!.Hazards)
        {
            var transform = host.Flow.Presenter.HazardObjects[pose.Id].Transform;
            Assert.AreEqual(pose.Bounds.Center, transform.Position);
            Assert.AreEqual(pose.Bounds.HalfExtents * 2, transform.Scale);
        }
        var before = host.Flow.Snapshot;
        host.Flow.Pause(); for (int i = 0; i < 50; i++) host.Advance(new(true), 0.02);
        Assert.AreSame(before, host.Flow.Snapshot);
        host.Flow.MainMenu(); host.Flow.Start("level1"); host.Advance(new(true), 0);
        Assert.AreEqual(tropicalLight, host.Scene.Lighting.AmbientColor);
    }

    [TestMethod]
    public void IcePressDropsThroughItsDangerZoneAndHitsBetweenClearTickEndpoints()
    {
        var source = WinterLevels.IcicleCanyon().Hazards[0];
        var low = HazardMotion.Evaluate(source, 3 * Math.PI / (2 * source.AngularSpeed)).Bounds;
        var high = HazardMotion.Evaluate(source, Math.PI / (2 * source.AngularSpeed)).Bounds;
        Assert.IsGreaterThan(0f, low.Center.Y - low.HalfExtents.Y);
        Assert.IsGreaterThan(3f, high.Center.Y - high.HalfExtents.Y);
        var settings = new SimulationSettings(maxCollisionSubsteps: 128);
        var press = new HazardDefinition("fast-press", new(0, 0.5f, 0), Vector3.UnitY, Vector3.Zero,
            new(0.1f), Quaternion.Identity, MathF.PI / settings.FixedStepSeconds, MathF.PI / 2,
            HazardResponse.Fail, HazardMotionKind.Shuttle, 2);
        Vector3 ball = new(0, 0.5f, 0);
        foreach (double time in new[] { 0d, settings.FixedStepSeconds })
            Assert.IsGreaterThan(settings.BallRadius, Vector3.Distance(ball, HazardMotion.Evaluate(press, time).Bounds.ClosestPoint(ball)));
        Assert.IsTrue(HazardCollision.IntersectsPath(press, [new(ball, ball, 0, 1)], 0, settings));
    }
}
