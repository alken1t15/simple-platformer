using Microsoft.VisualStudio.TestTools.UnitTesting;
using Platformer.App;
using Platformer.Core;

namespace Platformer.Tests;

[TestClass]
public sealed class TropicalMenuThemeTests
{
    [TestMethod]
    public void ThemeKeepsTextAndControlGeometryAndReusesBackgroundTextures()
    {
        var catalog = GameLevels.CreateCatalog();
        var flow = new LevelFlow(catalog, SimulationSettings.Default, new());
        var menu = new MenuController(flow, catalog, visual: true);
        foreach (var screen in new[] { AppScreen.MainMenu, AppScreen.Settings, AppScreen.SkinSelect, AppScreen.LevelSelect })
        {
            if (screen == AppScreen.Settings) flow.OpenSettings();
            if (screen == AppScreen.SkinSelect) flow.OpenSkins();
            if (screen == AppScreen.LevelSelect) flow.OpenLevelSelect();
            foreach (var (w, h) in new[] { (800f, 450f), (1920f, 1080f), (600f, 900f) })
            {
                var view = menu.BuildView(w, h);
                var standard = MenuOverlayAdapter.Build(view);
                var themed = MenuOverlayAdapter.Build(view, true);
                var next = MenuOverlayAdapter.Build(menu.BuildView(w, h), true);
                Assert.AreEqual(standard.Texts.Length, themed.Texts.Length);
                for (int i = 0; i < themed.Texts.Length; i++)
                {
                    Assert.AreEqual(standard.Texts[i].Text, themed.Texts[i].Text);
                    Assert.AreEqual(standard.Texts[i].Position, themed.Texts[i].Position);
                    Assert.AreEqual(standard.Texts[i].PixelHeight, themed.Texts[i].PixelHeight);
                    Assert.IsGreaterThan(0.7f, themed.Texts[i].Color.G, "Тёмная карточка требует светлой надписи");
                }
                for (int i = 0; i < view.Panels.Count; i++)
                {
                    Assert.AreSame(themed.Images[i].Texture, next.Images[i].Texture);
                    var bounds = themed.Images[i].Bounds;
                    Assert.AreEqual(view.Panels[i].Bounds.X, bounds.X);
                    Assert.AreEqual(view.Panels[i].Bounds.Y, bounds.Y);
                    Assert.AreEqual(view.Panels[i].Bounds.Width, bounds.Width);
                    Assert.AreEqual(view.Panels[i].Bounds.Height, bounds.Height);
                }
                foreach (var control in view.Controls)
                {
                    int index = view.Panels.ToList().FindIndex(p => p.Kind == MenuPanelKind.Control && p.Bounds == control.Bounds);
                    Assert.IsGreaterThanOrEqualTo(0, index);
                    Assert.AreEqual(0, themed.Images[index].Texture.Pixels[3], "Края кнопки прозрачны");
                }
            }
            if (screen != AppScreen.MainMenu) flow.Back();
        }
    }

    [TestMethod]
    public void SelectionAndLockedCardsRemainDistinct()
    {
        var catalog = GameLevels.CreateCatalog();
        var flow = new LevelFlow(catalog, SimulationSettings.Default, new());
        var menu = new MenuController(flow, catalog);
        var view = menu.BuildView(1280, 720);
        var overlay = MenuOverlayAdapter.Build(view, true);
        int PanelFor(MenuControl control, MenuView frame) => frame.Panels.ToList()
            .FindIndex(p => p.Kind == MenuPanelKind.Control && p.Bounds == control.Bounds);
        var selected = overlay.Images[PanelFor(view.Controls.Single(c => c.Selected), view)].Texture;
        var normal = overlay.Images[PanelFor(view.Controls.First(c => !c.Selected), view)].Texture;
        Assert.AreNotSame(selected, normal);
        flow.OpenLevelSelect();
        view = menu.BuildView(1280, 720);
        overlay = MenuOverlayAdapter.Build(view, true);
        var locked = overlay.Images[PanelFor(view.Controls.Single(c => c.Locked), view)].Texture;
        Assert.AreNotSame(selected, locked);
        Assert.AreNotSame(normal, locked);
    }
}
