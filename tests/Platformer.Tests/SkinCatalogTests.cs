using Engine3D.Core;
using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Platformer.App;
using Platformer.Core;

namespace Platformer.Tests;

[TestClass]
public sealed class SkinCatalogTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void AllFourteenSkinsCanBePreviewedSelectedAndRetainedThroughRestart(bool visual)
    {
        CollectionAssert.AreEqual(new[] { "classic-blue", "orange", "emerald", "violet", "basketball", "football", "neon", "aurora", "champion",
            "watermelon", "porcelain", "magma", "disco", "voyager" },
            GameSkins.All.Select(s => s.Id).ToArray());
        var settings = new UserSettings();
        var catalog = GameLevels.CreateCatalog();
        var progress = new PlayerProgress(catalog);
        foreach (var level in catalog.Levels) progress.RecordCompletion(level.Id, level.Coins.Count);
        var host = new GameplayHost(catalog, SimulationSettings.Default, settings, visual: visual, progress: progress);
        Click("skins");
        foreach (var skin in GameSkins.All)
        {
            foreach (var size in new[] { new Vector2(800, 450), new Vector2(1920, 1080) })
            {
                host.RefreshView(size.X, size.Y);
                var overlay = MenuOverlayAdapter.Build(host.View, visual);
                var previewBounds = host.View.Controls.Where(c => c.PreviewBounds is not null)
                    .Select(c => c.PreviewBounds!.Value).ToArray();
                Assert.HasCount(14, overlay.Images.ToArray().Where(image => previewBounds.Any(b =>
                    image.Bounds == new UiRect(b.X, b.Y, b.Width, b.Height))).ToArray());
                var control = host.View.Controls.Single(c => c.Id == "skin:" + skin.Id);
                Assert.IsNotNull(control.PreviewBounds);
                foreach (var card in host.View.Controls)
                {
                    Assert.IsGreaterThanOrEqualTo(0f, card.Bounds.Y);
                    Assert.IsLessThanOrEqualTo(size.Y, card.Bounds.Y + card.Bounds.Height);
                    var center = new Vector2(card.Bounds.X + card.Bounds.Width / 2, card.Bounds.Y + card.Bounds.Height / 2);
                    Assert.AreEqual(1, host.View.Controls.Count(other => other.Bounds.Contains(center)));
                }
            }
            Click("skin:" + skin.Id);
            Assert.AreEqual(skin.Id, settings.SkinId);
            Assert.AreSame(skin.Preview, skin.Preview);
        }
        Click("back"); Click("play");
        var initial = host.Flow.Snapshot!.Ball;
        Assert.AreEqual(0.5f, initial.Radius);
        var mesh = host.Flow.Presenter!.BallObject.Mesh;
        foreach (var skin in GameSkins.All)
        {
            settings.SkinId = skin.Id;
            host.Flow.Restart();
            host.Advance(new(true), 0);
            Assert.AreEqual(initial, host.Flow.Snapshot!.Ball);
            Assert.AreSame(mesh, host.Flow.Presenter!.BallObject.Mesh);
            Assert.AreEqual(skin.Id, host.Flow.UserSettings.SkinId);
        }

        void Click(string id)
        {
            host.Advance(new(true), 0);
            var b = host.View.Controls.Single(c => c.Id == id).Bounds;
            host.Advance(new(true, AppButtons.PointerLeft, AppButtons.PointerLeft,
                PointerPosition: new(b.X + b.Width / 2, b.Y + b.Height / 2)), 0);
            host.Advance(new(true), 0);
        }
    }

    [TestMethod]
    public void SportsPatternsHaveContrastingPanelsAndPreviewCyclesThroughEverySkin()
    {
        foreach (string id in new[] { "basketball", "football" })
        {
            var texture = GameSkins.Get(id).Material.Texture!;
            var pixels = texture.Pixels.ToArray();
            Assert.IsTrue(Enumerable.Range(0, texture.Width * texture.Height).Any(i => pixels[i * 4] < 50));
            Assert.IsTrue(Enumerable.Range(0, texture.Width * texture.Height).Any(i => pixels[i * 4] > 200));
        }
        var preview = new PresenterPreview();
        foreach (var skin in GameSkins.All)
        {
            Assert.AreEqual(skin.Id, preview.Skin.Id);
            preview.ToggleSkin();
        }
        Assert.AreEqual(GameSkins.All[0].Id, preview.Skin.Id);
    }
}
