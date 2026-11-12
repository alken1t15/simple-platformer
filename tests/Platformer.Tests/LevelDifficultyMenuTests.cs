using Engine3D.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Platformer.App;
using Platformer.Core;

namespace Platformer.Tests;

[TestClass]
public sealed class LevelDifficultyMenuTests
{
    [TestMethod]
    [DataRow(false)] [DataRow(true)]
    public void LevelCardsUseColoredDifficultyAndOnlyGraphicStarsForResults(bool visual)
    {
        var catalog = GameLevels.CreateCatalog();
        var progress = new PlayerProgress(catalog);
        progress.RecordCompletion("level1", 1);
        progress.RecordCompletion("level2", catalog.Get("level2").Coins.Count);
        progress.RecordCompletion("jump-circuit", catalog.Get("jump-circuit").Coins.Count / 2);
        var saved = progress.BestCoins.ToArray();
        var flow = new LevelFlow(catalog, SimulationSettings.Default, new(), progress: progress);
        var menu = new MenuController(flow, catalog, visual: visual);
        flow.OpenLevelSelect();
        foreach (var size in new[] { (800f, 450f), (1280f, 720f), (1920f, 1080f) })
        {
            var view = menu.BuildView(size.Item1, size.Item2);
            Assert.HasCount(39, view.Images);
            Assert.IsFalse(view.Texts.Any(t => t.Text.Contains('[') || t.Text.Contains("coins", StringComparison.OrdinalIgnoreCase)
                || t.Text.Contains("Not completed", StringComparison.Ordinal)));
            foreach (var control in view.Controls.Where(c => c.Kind == MenuControlKind.Level))
            {
                var label = view.Texts.Single(t => t.Kind == MenuTextKind.Difficulty && control.Bounds.Contains(t.Position));
                StringAssert.StartsWith(label.Text, "Difficulty: ");
                var images = view.Images.Where(i => control.Bounds.Contains(new(i.Bounds.X, i.Bounds.Y))).ToArray();
                Assert.HasCount(3, images);
                int rating = progress.StarsFor(control.Id["level:".Length..]);
                for (int i = 0; i < images.Length; i++)
                {
                    var texture = images[i].Texture;
                    int center = ((texture.Height / 2) * texture.Width + texture.Width / 2) * 4;
                    Assert.AreEqual(i < rating ? (byte)255 : (byte)0, texture.Pixels[center + 3]);
                }
                var text = MenuOverlayAdapter.Build(view, visual).Texts.ToArray().Single(t => t.Position == label.Position && t.Text == label.Text);
                Assert.AreEqual(label.Color, text.Color);
            }
            Assert.IsTrue(view.Texts.Any(t => t.Text.Contains("/12 stars on levels 1-4", StringComparison.Ordinal)));
            Assert.IsFalse(view.Controls.Single(c => c.Id == "level:sky-storm").Enabled);
            foreach (var expected in new[]
            {
                ("level1", "Easy", new ColorRgba(0.40f, 0.92f, 0.57f)),
                ("level2", "Normal", new ColorRgba(0.98f, 0.86f, 0.38f)),
                ("coral-ascent", "Hard", new ColorRgba(1f, 0.67f, 0.32f)),
                ("lava-causeway", "Very hard", new ColorRgba(1f, 0.47f, 0.28f)),
                ("clockwork-summit", "Expert", new ColorRgba(1f, 0.34f, 0.43f)),
            })
            {
                var card = view.Controls.Single(c => c.Id == "level:" + expected.Item1);
                var label = view.Texts.Single(t => t.Kind == MenuTextKind.Difficulty && card.Bounds.Contains(t.Position));
                Assert.AreEqual("Difficulty: " + expected.Item2, label.Text);
                Assert.AreEqual(expected.Item3, label.Color);
            }
        }
        CollectionAssert.AreEquivalent(saved, progress.BestCoins.ToArray());
    }

    [TestMethod]
    public void ExtraSkinsAreAvailableWithoutStarsAndSurviveSettingsRoundTrip()
    {
        var progress = new PlayerProgress(GameLevels.CreateCatalog());
        string path = Path.Combine(Path.GetTempPath(), "ball-extra-skins-" + Guid.NewGuid().ToString("N"), "settings.json");
        try
        {
            foreach (string id in new[] { "watermelon", "porcelain", "magma", "disco", "voyager" })
            {
                Assert.IsTrue(progress.IsSkinUnlocked(id));
                var stored = StoredSettings.Default with { SkinId = id };
                Assert.IsTrue(new SettingsStore(path).Save(stored));
                Assert.AreEqual(stored, new SettingsStore(path).Load(progress));
            }
            Assert.AreEqual(0, progress.TotalStars);
            Assert.IsFalse(progress.IsSkinUnlocked("champion"));
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
            if (Directory.Exists(Path.GetDirectoryName(path))) Directory.Delete(Path.GetDirectoryName(path)!);
        }
    }
}
