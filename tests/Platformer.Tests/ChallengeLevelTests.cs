using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Platformer.App;
using Platformer.Core;

namespace Platformer.Tests;

[TestClass]
public sealed class ChallengeLevelTests
{
    [TestMethod]
    public void FiveChallengesAreInsertedBeforeTheOriginalFinalAndPreserveOldUnlock()
    {
        var catalog = GameLevels.CreateCatalog();
        Assert.HasCount(13, catalog.Levels);
        CollectionAssert.AreEqual(ChallengeRoutes.Ids, catalog.Levels.Skip(4).Take(5).Select(l => l.Id).ToArray());
        Assert.AreEqual(GameLevels.FinalLevelId, catalog.Levels[^1].Id);
        Assert.AreEqual(162, catalog.Levels.Sum(l => l.Coins.Count));
        var progress = new PlayerProgress(catalog);
        foreach (string id in ChallengeRoutes.Ids)
        {
            Assert.IsTrue(progress.IsLevelUnlocked(id));
            progress.RecordCompletion(id, catalog.Get(id).Coins.Count);
        }
        Assert.AreEqual(15, progress.TotalStars);
        Assert.AreEqual(0, progress.BaseStars);
        Assert.AreEqual(39, progress.MaximumStars);
        Assert.IsFalse(progress.IsLevelUnlocked(GameLevels.FinalLevelId));
        foreach (var level in catalog.Levels.Take(4)) progress.RecordCompletion(level.Id, level.Coins.Count);
        Assert.IsTrue(progress.IsLevelUnlocked(GameLevels.FinalLevelId));
    }

    [TestMethod]
    public void PreviousFiveLevelSaveKeepsAllRatingsAndFinalUnlockInExpandedCatalog()
    {
        string directory = Path.Combine(Path.GetTempPath(), "ball-old-save-" + Guid.NewGuid().ToString("N"));
        string path = Path.Combine(directory, "progress.json");
        try
        {
            var oldCatalog = new LevelCatalog([TrainingTrack.Create(), TurningMaze.Create(), JumpCircuit.Create(),
                IslandCircuit.Create(), SkyStorm.Create()]);
            var oldProgress = new PlayerProgress(oldCatalog);
            foreach (var level in oldCatalog.Levels) oldProgress.RecordCompletion(level.Id, level.Coins.Count);
            Assert.IsTrue(new ProgressStore(path).Save(oldProgress));
            var loaded = new ProgressStore(path).Load(GameLevels.CreateCatalog());
            CollectionAssert.AreEquivalent(oldProgress.BestCoins.ToArray(), loaded.BestCoins.ToArray());
            Assert.AreEqual(15, loaded.TotalStars);
            Assert.AreEqual(12, loaded.BaseStars);
            Assert.IsTrue(loaded.IsLevelUnlocked(GameLevels.FinalLevelId));
            foreach (string id in ChallengeRoutes.Ids) Assert.AreEqual(0, loaded.StarsFor(id));
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
            if (Directory.Exists(directory)) Directory.Delete(directory);
        }
    }

    [TestMethod]
    public void DirectTwelfthLevelOffersJourneyEndWithoutBypassingOriginalFinalGate()
    {
        var host = new GameplayHost(GameLevels.CreateCatalog(), SimulationSettings.Default, new UserSettings());
        host.Flow.Start("clockwork-summit");
        var driver = new ProductionRouteTests.Driver(host);
        driver.Press(AppButtons.None);
        ChallengeRoutes.Run(driver, "clockwork-summit");
        Assert.AreEqual(AppScreen.LevelResult, host.Flow.Screen);
        Assert.AreEqual(0, host.Flow.Progress.BaseStars);
        driver.Press(AppButtons.Enter);
        Assert.AreEqual(AppScreen.GameComplete, host.Flow.Screen);
        Assert.IsFalse(host.Flow.Progress.IsLevelUnlocked(GameLevels.FinalLevelId));
    }

    [TestMethod]
    [DataRow("coral-ascent", false)] [DataRow("coral-ascent", true)]
    [DataRow("windmill-pass", false)] [DataRow("windmill-pass", true)]
    [DataRow("lava-causeway", false)] [DataRow("lava-causeway", true)]
    [DataRow("crumble-canyon", false)] [DataRow("crumble-canyon", true)]
    [DataRow("temple-gauntlet", false)] [DataRow("temple-gauntlet", true)]
    [DataRow("pendulum-gorge", false)] [DataRow("pendulum-gorge", true)]
    [DataRow("piston-foundry", false)] [DataRow("piston-foundry", true)]
    [DataRow("clockwork-summit", false)] [DataRow("clockwork-summit", true)]
    public void ChallengeCanBeCompletedWithAllCoinsThroughRealHostInput(string id, bool visual)
    {
        var catalog = GameLevels.CreateCatalog();
        var host = new GameplayHost(catalog, SimulationSettings.Default, new UserSettings(), visual: visual);
        host.Flow.Start(id);
        var driver = new ProductionRouteTests.Driver(host);
        driver.Press(AppButtons.None); // Применить смену экрана и rearm перед первым удержанием WASD.
        ChallengeRoutes.Run(driver, id);
        Assert.AreEqual(AppScreen.LevelResult, host.Flow.Screen, id);
        Assert.AreEqual(12, host.Flow.Snapshot!.CoinCount, id);
        Assert.AreEqual(3, host.Flow.Progress.StarsFor(id));
        host.Flow.Restart();
        Assert.AreEqual(0, host.Flow.Snapshot!.CoinCount);
        Assert.IsTrue(host.Flow.Snapshot.Platforms.All(p => !p.Collapsed && p.TriggeredAtSeconds is null));
    }

    [TestMethod]
    [DataRow(800f, 450f)] [DataRow(1280f, 720f)] [DataRow(1920f, 1080f)]
    public void ThirteenLevelCardsAndBackFitAndEveryUnlockedCardStartsItsOwnLevel(float width, float height)
    {
        var catalog = GameLevels.CreateCatalog();
        foreach (var level in catalog.Levels)
        {
            var progress = new PlayerProgress(catalog);
            foreach (var original in catalog.Levels.Take(4)) progress.RecordCompletion(original.Id, original.Coins.Count);
            var flow = new LevelFlow(catalog, SimulationSettings.Default, new UserSettings(), progress: progress);
            var menu = new MenuController(flow, catalog, visual: true);
            flow.OpenLevelSelect();
            var view = menu.BuildView(width, height);
            Assert.HasCount(14, view.Controls);
            foreach (var control in view.Controls)
            {
                Assert.IsGreaterThanOrEqualTo(0f, control.Bounds.X);
                Assert.IsGreaterThanOrEqualTo(0f, control.Bounds.Y);
                Assert.IsLessThanOrEqualTo(width, control.Bounds.X + control.Bounds.Width);
                Assert.IsLessThanOrEqualTo(height, control.Bounds.Y + control.Bounds.Height);
            }
            var card = view.Controls.Single(c => c.Id == $"level:{level.Id}").Bounds;
            var point = new Vector2(card.X + card.Width / 2, card.Y + card.Height / 2);
            menu.Handle(new(default, false, false, new(0, 0, false, false, true, point)), width, height);
            Assert.AreEqual(AppScreen.Playing, flow.Screen);
            Assert.AreEqual(level.Id, flow.Snapshot!.LevelId);
        }
    }
}
