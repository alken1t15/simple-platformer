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
    public void ContactPatchesStayOnVisibleRoadGeometryAcrossAllTwentyLevels()
    {
        foreach(var level in GameLevels.CreateCatalog().Levels)
        {
            var presenter=new GameScenePresenter(level,true);
            var tops=presenter.Scene.Objects.Where(o=>o.Transform.Position==Vector3.Zero && o.Transform.Scale==Vector3.One
                && o.Material.Texture is {Width:128,Height:64} or {Width:128,Height:128}).Select(o=>o.Mesh).ToArray();
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
                    foreach(var item in presenter.Scene.Objects.Where(o=>o.Material.Texture is {Width:192,Height:192}))
                    for(int i=0;i<item.Mesh.IndexCount;i+=3)
                    {
                        var a=item.Mesh.Vertices[(int)item.Mesh.Indices[i]];var b=item.Mesh.Vertices[(int)item.Mesh.Indices[i+1]];var c=item.Mesh.Vertices[(int)item.Mesh.Indices[i+2]];
                        Vector3 point=(a.Position+b.Position+c.Position)/3-a.Normal*0.002f;
                        Assert.IsTrue(tops.Any(mesh=>Covers(mesh,point)),level.Id+" "+surface.Id+" contact patch outside visible road");
                    }
                }
            }
        }
        static bool Covers(MeshData mesh,Vector3 point)
        {
            for(int i=0;i<mesh.IndexCount;i+=3)
            {
                var a=mesh.Vertices[(int)mesh.Indices[i]].Position;var b=mesh.Vertices[(int)mesh.Indices[i+1]].Position;var c=mesh.Vertices[(int)mesh.Indices[i+2]].Position;
                var u=b-a;var v=c-a;var w=point-a;var n=Vector3.Cross(u,v);
                if(n.LengthSquared()<1e-12f||MathF.Abs(Vector3.Dot(w,Vector3.Normalize(n)))>0.0001f)continue;
                float uu=Vector3.Dot(u,u),uv=Vector3.Dot(u,v),vv=Vector3.Dot(v,v),wu=Vector3.Dot(w,u),wv=Vector3.Dot(w,v),denom=uu*vv-uv*uv;
                if(MathF.Abs(denom)<1e-12f)continue;
                float x=(wu*vv-wv*uv)/denom,y=(wv*uu-wu*uv)/denom;
                if(x>=-0.0001f&&y>=-0.0001f&&x+y<=1.0001f)return true;
            }
            return false;
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
        Assert.HasCount(1,shadows);
        var shadow=shadows[0];
        foreach(var vertex in shadow.Mesh.Vertices)
            Assert.IsGreaterThanOrEqualTo(2f-0.0001f,vertex.Position.X,"Second platform must not cover the first platform's top");
    }
}