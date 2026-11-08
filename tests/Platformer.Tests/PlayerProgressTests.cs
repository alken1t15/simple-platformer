using Microsoft.VisualStudio.TestTools.UnitTesting;
using Platformer.App;
using Platformer.Core;

namespace Platformer.Tests;

[TestClass]
public sealed class PlayerProgressTests
{
    [TestMethod]
    [DataRow(0, 15, 1)]
    [DataRow(7, 15, 1)]
    [DataRow(8, 15, 2)]
    [DataRow(14, 15, 2)]
    [DataRow(15, 15, 3)]
    [DataRow(5, 11, 1)]
    [DataRow(6, 11, 2)]
    [DataRow(5, 10, 2)]
    public void RatingsUseCompletedAttemptAndRoundHalfUp(int coins, int total, int expected) =>
        Assert.AreEqual(expected, PlayerProgress.Rating(coins, total));

    [TestMethod]
    public void ElevenStarsKeepFinalLockedAndTwelfthOpensItWithoutSpendingRatings()
    {
        var catalog = GameLevels.CreateCatalog();
        var progress = new PlayerProgress(catalog);
        foreach (var level in catalog.Levels.Take(3)) progress.RecordCompletion(level.Id, level.Coins.Count);
        progress.RecordCompletion("island-circuit", 6);
        Assert.AreEqual(11, progress.TotalStars);
        Assert.IsFalse(progress.IsLevelUnlocked(GameLevels.FinalLevelId));
        Assert.IsTrue(progress.IsSkinUnlocked("neon"));
        Assert.IsTrue(progress.IsSkinUnlocked("aurora"));
        Assert.IsFalse(progress.IsSkinUnlocked("champion"));
        var flow = new LevelFlow(catalog, SimulationSettings.Default, new UserSettings(), progress: progress);
        Assert.ThrowsExactly<InvalidOperationException>(() => flow.Start(GameLevels.FinalLevelId));
        Assert.AreEqual(AppScreen.MainMenu, flow.Screen);
        progress.RecordCompletion("island-circuit", 11);
        Assert.IsTrue(progress.IsLevelUnlocked(GameLevels.FinalLevelId));
        Assert.IsFalse(progress.RecordCompletion("island-circuit", 0));
        Assert.AreEqual(12, progress.TotalStars);
        flow.Start(GameLevels.FinalLevelId);
        Assert.AreEqual(GameLevels.FinalLevelId, flow.Snapshot!.LevelId);
        progress.RecordCompletion(GameLevels.FinalLevelId, catalog.Get(GameLevels.FinalLevelId).Coins.Count);
        Assert.AreEqual(15, progress.TotalStars);
        Assert.IsTrue(progress.IsSkinUnlocked("champion"));
        flow.MainMenu();
        Assert.AreEqual(15, progress.TotalStars);
    }

    [TestMethod]
    public void SaveRoundTripPreservesBestAndDamagedOrInvalidFileStartsSafely()
    {
        string directory = Path.Combine(Path.GetTempPath(), "ball-progress-" + Guid.NewGuid().ToString("N"));
        try
        {
            string path = Path.Combine(directory, "progress.json");
            var store = new ProgressStore(path);
            var catalog = GameLevels.CreateCatalog();
            var progress = store.Load(catalog);
            Assert.AreEqual(0, progress.TotalStars);
            foreach (var level in catalog.Levels) progress.RecordCompletion(level.Id, level.Coins.Count);
            Assert.IsTrue(store.Save(progress), store.Diagnostic);
            var loaded = new ProgressStore(path).Load(catalog);
            CollectionAssert.AreEquivalent(progress.BestCoins.ToArray(), loaded.BestCoins.ToArray());
            Assert.AreEqual(15, loaded.TotalStars);
            Assert.HasCount(1, Directory.GetFiles(directory)); // Временный файл не остаётся.
            foreach (string invalid in new[] { "{broken", "{\"Version\":2,\"BestCoins\":{}}",
                "{\"Version\":1,\"BestCoins\":{\"level1\":-1}}" })
            {
                File.WriteAllText(path, invalid);
                Assert.AreEqual(0, store.Load(catalog).TotalStars);
                Assert.IsNotNull(store.Diagnostic);
            }
            // Ошибка записи не уничтожает уже имеющийся файл и не бросает в игровом Step.
            File.WriteAllText(path, "unchanged");
            var blocked = new ProgressStore(Path.Combine(path, "child.json"));
            Assert.IsFalse(blocked.Save(progress));
            Assert.AreEqual("unchanged", File.ReadAllText(path));
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true); }
    }

    [TestMethod]
    public void CompletionIsRecordedAtFinishWithoutNextAndRetryCannotFarmStars()
    {
        var level = new LevelDefinition("short", new(0, 0.501f, 0), -5,
            new(new(0.2f, 0.75f, 0), new(0.1f, 0.75f, 1), System.Numerics.Quaternion.Identity),
            [new("floor", new(new(0, -0.2f, 0), new(3, 0.2f, 3), System.Numerics.Quaternion.Identity))],
            [new("coin", new(2, 0.8f, 2), 0.2f)], []);
        var flow = new LevelFlow(new([level]), SimulationSettings.Default, new UserSettings());
        int changes = 0;
        flow.ProgressChanged += () => changes++;
        flow.Start(level.Id);
        flow.Step(default);
        Assert.AreEqual(AppScreen.LevelResult, flow.Screen);
        Assert.AreEqual(1, flow.Progress.TotalStars);
        Assert.AreEqual(1, changes);
        flow.Restart(); flow.Step(default);
        Assert.AreEqual(1, flow.Progress.TotalStars);
        Assert.AreEqual(1, changes);
        flow.MainMenu();
        Assert.AreEqual(1, flow.Progress.TotalStars);
    }

    [TestMethod]
    public void LockedCardsShowRequirementsAndCannotStartFinalOrApplyRewardByClickOrKeyboard()
    {
        var catalog = GameLevels.CreateCatalog();
        var flow = new LevelFlow(catalog, SimulationSettings.Default, new UserSettings());
        var menu = new MenuController(flow, catalog);
        void Click(string id)
        {
            var b = menu.BuildView(800, 450).Controls.Single(control => control.Id == id).Bounds;
            menu.Handle(new(default, false, false, new(0, 0, false, false, true,
                new(b.X + b.Width / 2, b.Y + b.Height / 2))), 800, 450);
        }
        Click("levels"); Click("level:sky-storm");
        Assert.IsFalse(menu.BuildView(800, 450).Controls.Single(control => control.Id == "level:sky-storm").Enabled);
        menu.Handle(new(default, false, false, new(0, 0, true, false, false, default)), 800, 450);
        Assert.AreEqual(AppScreen.LevelSelect, flow.Screen);
        Assert.IsNull(flow.Snapshot);
        Click("back"); Click("skins"); Click("skin:neon");
        Assert.AreEqual("classic-blue", flow.UserSettings.SkinId);
        Assert.IsTrue(menu.BuildView(800, 450).Texts.Any(text => text.Text == "Locked: 6 stars"));
        foreach (var level in catalog.Levels.Take(2)) flow.Progress.RecordCompletion(level.Id, level.Coins.Count);
        Click("skin:neon");
        Assert.AreEqual("neon", flow.UserSettings.SkinId);
        Assert.AreEqual(6, flow.Progress.TotalStars);
        Assert.IsFalse(flow.Progress.IsSkinUnlocked("aurora"));
        foreach (var level in catalog.Levels.Skip(2).Take(2)) flow.Progress.RecordCompletion(level.Id, level.Coins.Count);
        Click("back"); Click("levels"); Click("level:sky-storm");
        Assert.AreEqual(AppScreen.Playing, flow.Screen);
        Assert.AreEqual(GameLevels.FinalLevelId, flow.Snapshot!.LevelId);
    }
}
