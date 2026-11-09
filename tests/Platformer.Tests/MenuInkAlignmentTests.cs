using System.Security.Cryptography;
using Engine3D.OpenGL;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Platformer.App;
using Platformer.Core;

namespace Platformer.Tests;

[TestClass]
public sealed class MenuInkAlignmentTests
{
    [TestMethod]
    public void CardTextGroupsStayCenteredBesideTheirPictures()
    {
        var catalog = GameLevels.CreateCatalog();
        var flow = new LevelFlow(catalog, SimulationSettings.Default, new());
        var menu = new MenuController(flow, catalog, visual: true);
        foreach (bool skins in new[] { false, true })
        {
            if (skins) flow.OpenSkins(); else flow.OpenLevelSelect();
            foreach (var control in menu.BuildView(1280, 720).Controls.Where(c => c.Kind is MenuControlKind.Level or MenuControlKind.Skin))
            {
                var lines = menu.BuildView(1280, 720).Texts.Where(t => control.Bounds.Contains(t.Position)).ToArray();
                Assert.HasCount(2, lines);
                var tops = lines.Select(t => t.Position.Y + OverlayTextMetrics.MeasureInkBounds(t.Text, t.PixelHeight).Y).ToArray();
                var bottoms = lines.Select(t => t.Position.Y + OverlayTextMetrics.MeasureInkBounds(t.Text, t.PixelHeight).Y
                    + OverlayTextMetrics.MeasureInkBounds(t.Text, t.PixelHeight).Height).ToArray();
                Assert.AreEqual(control.Bounds.Y + control.Bounds.Height / 2, (tops.Min() + bottoms.Max()) / 2, 1e-4f);
                foreach (var line in lines)
                    Assert.AreEqual(control.Bounds.X + (skins ? 60 : 14), line.Position.X);
            }
            flow.Back();
        }
    }

    [TestMethod]
    public void SingleLineButtonInkIsVerticallyCenteredWhileHorizontalInsetsStayUnchanged()
    {
        var catalog = GameLevels.CreateCatalog();
        var flow = new LevelFlow(catalog, SimulationSettings.Default, new());
        var menu = new MenuController(flow, catalog, visual: true);
        foreach (bool settings in new[] { false, true })
        {
            if (settings) flow.OpenSettings();
            foreach (var (w, h) in new[] { (800f, 450f), (1280f, 720f), (1920f, 1080f) })
            {
                var view = menu.BuildView(w, h);
                foreach (var control in view.Controls)
                {
                    var text = view.Texts.Single(t => t.Text == control.Label);
                    var ink = OverlayTextMetrics.MeasureInkBounds(text.Text, text.PixelHeight);
                    Assert.AreEqual(control.Bounds.Y + control.Bounds.Height / 2,
                        text.Position.Y + ink.Y + ink.Height / 2, 1e-4f, control.Id);
                    Assert.AreEqual(control.Bounds.X + 20 * w / 1280, text.Position.X, 1e-4f,
                        "Горизонтальное выравнивание остаётся слева");
                }
            }
        }
    }

    [TestMethod]
    public void RuntimeWindowIconIsPackagedUnchangedFromTheExistingIcoPngPayload()
    {
        var bytes = File.ReadAllBytes(WindowBranding.IconPath);
        Assert.AreEqual("0841C72B80CEC5C32900D4B4098ED309911BB1062D38E90CFBEC23ECAF6273A3",
            Convert.ToHexString(SHA256.HashData(bytes)));
        Assert.AreEqual(256, System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(16, 4)));
        Assert.AreEqual(256, System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(20, 4)));
    }
}
