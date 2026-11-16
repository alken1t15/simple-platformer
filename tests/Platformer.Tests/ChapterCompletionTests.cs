using System.Numerics;
using Engine3D.OpenGL;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Platformer.App;
using Platformer.Core;

namespace Platformer.Tests;

[TestClass]
public sealed class ChapterCompletionTests
{
    [TestMethod]
    [DataRow(false,false)][DataRow(false,true)][DataRow(true,false)][DataRow(true,true)]
    public void NextAtTheEndOfAChapterShowsACompactDialogWithWorkingActions(bool winter,bool visual)
    {
        string id=winter?"winter-snowbound":"level1";
        var level=new LevelDefinition(id,new(0,.501f,0),-3,
            new(new(.2f,.75f,0),new(.1f,.75f,1),Quaternion.Identity),
            [new("floor",new(new(0,-.2f,0),new(4,.2f,3),Quaternion.Identity))],[],[]);
        var catalog=new LevelCatalog([level]);
        var flow=new LevelFlow(catalog,SimulationSettings.Default,new(),visual:visual);
        var menu=new MenuController(flow,catalog,visual:visual);
        flow.Start(id);flow.Step(default);
        Assert.AreEqual(AppScreen.LevelResult,flow.Screen);
        Click("next");Assert.AreEqual(AppScreen.GameComplete,flow.Screen);
        var progress=flow.Progress.TotalStars;
        foreach(var size in new[]{(800f,450f),(1280f,720f),(1920f,1080f)})
        {
            var view=menu.BuildView(size.Item1,size.Item2);
            float scale=MathF.Min(size.Item1/1280,size.Item2/720);
            var sheet=view.Panels.Single(p=>p.Kind==MenuPanelKind.Sheet).Bounds;
            Assert.AreEqual(600*scale,sheet.Width,.001f);Assert.AreEqual(320*scale,sheet.Height,.001f);
            Assert.AreEqual(size.Item1/2,sheet.X+sheet.Width/2,.001f);
            Assert.AreEqual(size.Item2/2,sheet.Y+sheet.Height/2,.001f);
            Assert.HasCount(2,view.Controls);Assert.HasCount(2,view.Images);
            Assert.IsTrue(view.Controls[0].Selected);
            foreach(var text in view.Texts)
            {
                var ink=OverlayTextMetrics.MeasureInkBounds(text.Text,text.PixelHeight);
                Assert.IsTrue(sheet.Contains(text.Position+new Vector2(ink.X,ink.Y)),text.Text);
                Assert.IsTrue(sheet.Contains(text.Position+new Vector2(ink.X+ink.Width,ink.Y+ink.Height)),text.Text);
            }
            foreach(var control in view.Controls)
            {
                Assert.IsTrue(sheet.Contains(new(control.Bounds.X,control.Bounds.Y)));
                Assert.IsTrue(sheet.Contains(new(control.Bounds.X+control.Bounds.Width,control.Bounds.Y+control.Bounds.Height)));
            }
            MenuOverlayAdapter.Build(view,visual);
        }
        Assert.IsEmpty(menu.BuildView(0,450).Controls);
        Assert.IsEmpty(menu.BuildView(800,0).Texts);
        Click("levels");Assert.AreEqual(AppScreen.LevelSelect,flow.Screen);
        Assert.AreEqual(progress,flow.Progress.TotalStars);
        flow.Back();flow.Start(id);flow.Step(default);Click("next");Click("main-menu");
        Assert.AreEqual(AppScreen.MainMenu,flow.Screen);Assert.AreEqual(progress,flow.Progress.TotalStars);
        void Click(string button)
        {
            var rect=menu.BuildView(1280,720).Controls.Single(c=>c.Id==button).Bounds;
            menu.Handle(new(default,false,false,new(0,0,false,false,true,new(rect.X+rect.Width/2,rect.Y+rect.Height/2))),1280,720);
        }
    }
}
