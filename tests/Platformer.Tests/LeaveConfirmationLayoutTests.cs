using System.Numerics;
using Engine3D.OpenGL;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Platformer.App;
using Platformer.Core;

namespace Platformer.Tests;

[TestClass]
public sealed class LeaveConfirmationLayoutTests
{
    [TestMethod]
    [DataRow(false)] [DataRow(true)]
    public void CompactConfirmationFitsItsContentAndPreservesCancelAndLeaveActions(bool visual)
    {
        foreach (var (width, height) in new[] { (800f, 450f), (1280f, 720f), (1920f, 1080f) })
        {
            var catalog = GameLevels.CreateCatalog();
            var flow = new LevelFlow(catalog, SimulationSettings.Default, new(), visual: visual);
            var menu = new MenuController(flow, catalog, visual: visual);
            flow.Start("level1"); flow.Step(default); flow.Pause();
            var before = flow.Snapshot;
            void Click(string id)
            {
                var b = menu.BuildView(width, height).Controls.Single(c => c.Id == id).Bounds;
                menu.Handle(new(default, false, false, new(0, 0, false, false, true,
                    new(b.X + b.Width / 2, b.Y + b.Height / 2))), width, height);
            }
            Click("main-menu");
            var view = menu.BuildView(width, height);
            var sheet = view.Panels.Single(p => p.Kind == MenuPanelKind.Sheet).Bounds;
            Assert.AreEqual(560f / 260f, sheet.Width / sheet.Height, 0.001f);
            Assert.IsLessThan(height * 0.4f, sheet.Height);
            Assert.AreEqual(width / 2, sheet.X + sheet.Width / 2, 0.001f);
            Assert.AreEqual(height / 2, sheet.Y + sheet.Height / 2, 0.001f);
            Assert.HasCount(2, view.Controls);
            Assert.AreEqual("cancel-main-menu", view.Controls.Single(c => c.Selected).Id);
            foreach (var text in view.Texts)
            {
                var ink = OverlayTextMetrics.MeasureInkBounds(text.Text, text.PixelHeight);
                Assert.IsGreaterThanOrEqualTo(sheet.X, text.Position.X + ink.X);
                Assert.IsGreaterThanOrEqualTo(sheet.Y, text.Position.Y + ink.Y);
                Assert.IsLessThanOrEqualTo(sheet.X + sheet.Width, text.Position.X + ink.X + ink.Width);
                Assert.IsLessThanOrEqualTo(sheet.Y + sheet.Height, text.Position.Y + ink.Y + ink.Height);
            }
            foreach (var button in view.Controls)
            {
                Assert.IsTrue(view.Panels.Any(p => p.Kind == MenuPanelKind.Control && p.Bounds == button.Bounds));
                var text = view.Texts.Single(t => t.Text == button.Label);
                var ink = OverlayTextMetrics.MeasureInkBounds(text.Text, text.PixelHeight);
                Assert.AreEqual(button.Bounds.Y + button.Bounds.Height / 2, text.Position.Y + ink.Y + ink.Height / 2, 0.001f);
            }
            Assert.AreEqual(view.Texts.Count, MenuOverlayAdapter.Build(view, visual).Texts.Length);
            Assert.IsEmpty(menu.BuildView(0, 0).Controls);
            menu.Handle(new(default, false, false, new(0, 0, true, false, false, default)), width, height);
            Assert.AreEqual(AppScreen.Paused, flow.Screen);
            Assert.AreSame(before, flow.Snapshot);
            Click("main-menu");
            menu.Handle(new(default, false, false, new(0, 0, false, true, false, default)), width, height);
            Assert.AreSame(before, flow.Snapshot);
            Click("main-menu"); Click("confirm-main-menu");
            Assert.AreEqual(AppScreen.MainMenu, flow.Screen);
            Assert.IsNull(flow.Snapshot);
        }
    }
}
