using System.Numerics;
using Engine3D.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Platformer.App;
using Platformer.Core;
namespace Platformer.Tests;
[TestClass]
public sealed class SurfaceOverlayTests
{
    [TestMethod]
    public void WinterCrumblingIceHasAVisibleGapFromEverySolidPlatform()
    {
        foreach(var level in GameLevels.CreateCatalog().Levels.Where(l=>GameChapters.For(l.Id)==GameChapter.Winter))
        foreach(var ice in level.Surfaces.Where(s=>s.CollapseDelaySeconds>0))
        foreach(var solid in level.Surfaces.Where(s=>s.CollapseDelaySeconds==0))
        {
            var a=ice.Bounds;var b=solid.Bounds;
            float dx=MathF.Max(0,MathF.Abs(a.Center.X-b.Center.X)-a.HalfExtents.X-b.HalfExtents.X);
            float dz=MathF.Max(0,MathF.Abs(a.Center.Z-b.Center.Z)-a.HalfExtents.Z-b.HalfExtents.Z);
            Assert.IsGreaterThanOrEqualTo(1.3f,MathF.Sqrt(dx*dx+dz*dz),level.Id+" "+ice.Id+" / "+solid.Id);
        }
        var summit=GameLevels.CreateCatalog().Get("winter-aurora");
        var last=summit.Surfaces.Single(s=>s.Id=="last-ice").Bounds;
        var finish=summit.Surfaces.Single(s=>s.Id=="finish-platform").Bounds;
        Assert.AreEqual(1.8f,finish.Center.X-finish.HalfExtents.X-(last.Center.X+last.HalfExtents.X),0.001f);
    }    [TestMethod]
    public void NoBallShadowIsRenderedAcrossTheLevelCatalog()
    {
        foreach(var level in GameLevels.CreateCatalog().Levels)
        {
            var presenter=new GameScenePresenter(level,true);
            long tick=0;Guid session=Guid.NewGuid();
            foreach(var surface in level.Surfaces.Where(s=>!s.IsBarrier&&s.CollapseDelaySeconds==0&&s.Traction==1))
            {
                Vector3 normal=Vector3.Transform(Vector3.UnitY,surface.Bounds.Orientation);
                foreach(float offset in new[]{-0.65f,0f,0.65f})
                {
                    var center=surface.Bounds.ToWorld(new(surface.Bounds.HalfExtents.X*offset,surface.Bounds.HalfExtents.Y+0.501f,0));
                    double seconds=++tick*(double)SimulationSettings.Default.FixedStepSeconds;
                    var snapshot=new SessionSnapshot(session,level.Id,tick,seconds,SessionPhase.Running,
                        new(center,Vector3.Zero,0.5f,true,normal,0,0),[],level.Hazards.Select(h=>HazardMotion.Evaluate(h,seconds)).ToArray(),
                        level.Surfaces.Where(s=>s.CollapseDelaySeconds>0).Select(s=>new CrumblingPlatformState(s.Id,null,false)).ToArray());
                    presenter.Apply(snapshot,GameSkins.All[0]);
                    Assert.IsFalse(presenter.Scene.Objects.Any(o=>o.Material.Texture is {Width:192,Height:192}),
                        level.Id+" "+surface.Id+" must not render a ball shadow");
                }
            }
        }
    }

    [TestMethod]
    public void TreeGarlandLightsPulseAndPauseWithTheWinterScene()
    {
        var host=new GameplayHost(GameLevels.CreateCatalog(),SimulationSettings.Default,new(),visual:true);
        host.Flow.Start("winter-aurora");host.Advance(new(true),0);
        var lights=host.Flow.Presenter!.Scene.Objects.Where(o=>o.Transform.Scale==new Vector3(0.075f)).Select(o=>o.Material).ToArray();
        Assert.IsGreaterThanOrEqualTo(48,lights.Length);
        var initial=lights.Select(m=>m.BaseColor).ToArray();
        for(int i=0;i<120;i++)host.Advance(new(true),SimulationSettings.Default.FixedStepSeconds);
        var on=lights.Select(m=>m.BaseColor).ToArray();
        Assert.IsTrue(initial.Where((color,i)=>color!=on[i]).Any());
        host.Flow.Pause();for(int i=0;i<120;i++)host.Advance(new(true),SimulationSettings.Default.FixedStepSeconds);
        CollectionAssert.AreEqual(on,lights.Select(m=>m.BaseColor).ToArray());
    }
    [TestMethod]
    public void SnowContactTextureNeverCoversMetalPressFoundations()
    {
        foreach(var level in GameLevels.CreateCatalog().Levels)
        foreach(var surface in level.Surfaces.Where(s=>s.Id.StartsWith("structure:")&&s.Id.EndsWith(":base")))
        {
            var presenter=new GameScenePresenter(level,true);
            var box=surface.Bounds;
            var ball=new BallState(box.ToWorld(new(0,box.HalfExtents.Y+0.501f,box.HalfExtents.Z-0.12f)),Vector3.Zero,0.5f,true,Vector3.UnitY,0,0);
            var foot=ball.Center-Vector3.UnitY*ball.Radius;
            if(level.Surfaces.Any(s=>!s.IsBarrier && Vector3.Distance(s.Bounds.ClosestPoint(foot),foot)<0.04f))continue;
            var poses=level.Hazards.Select(h=>HazardMotion.Evaluate(h,0)).ToArray();
            var snapshot=new SessionSnapshot(Guid.NewGuid(),level.Id,0,0,SessionPhase.Running,ball,[],poses,level.Surfaces.Where(s=>s.CollapseDelaySeconds>0).Select(s=>new CrumblingPlatformState(s.Id,null,false)).ToArray());
            presenter.Apply(snapshot,GameSkins.All[0]);
            var metal=presenter.SurfaceObjects[surface.Id];
            Assert.IsNotNull(metal.Material.Texture);
            Assert.IsFalse(presenter.Scene.Objects.Any(o=>o.Material.Texture is {Width:192,Height:192}),level.Id+" must not put a snow patch on metal");
        }
    }    [TestMethod]
    public void MovingContactPatchDoesNotPaintOverTheNeighbouringPlatform()
    {
        var level=new LevelDefinition("shadow-seam",new(2.5f,0.501f,0),-5,
            new(new(50,1,0),new(1),Quaternion.Identity),
            [new("first",new(new(0,-0.2f,0),new(2,0.2f,2),Quaternion.Identity)),
             new("second",new(new(3,-0.2f,0),new(2,0.2f,2),Quaternion.Identity))],[],[]);
        var presenter=new GameScenePresenter(level,true);
        presenter.Apply(new GameSession(level,SimulationSettings.Default).Snapshot,GameSkins.All[0]);
        var shadows=presenter.Scene.Objects.Where(o=>o.Material.Texture is {Width:192,Height:192}).ToArray();
        Assert.HasCount(0,shadows,"Тень шара отключена по замечаниям игрока");
    }
}
