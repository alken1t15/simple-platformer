using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Platformer.App;
using Platformer.Core;
namespace Platformer.Tests;
[TestClass]
public sealed class TrapStructureTests
{
    [TestMethod]
    public void EveryVisibleStructureUsesTheSameSolidBoundsAndStopsTheBall()
    {
        int tested=0;
        foreach(var level in GameLevels.CreateCatalog().Levels)
        {
            var presenter=new GameScenePresenter(level,true);
            foreach(var surface in level.Surfaces.Where(s=>s.Id.StartsWith("structure:")))
            {
                tested++;Assert.IsTrue(surface.IsBarrier);
                var item=presenter.SurfaceObjects[surface.Id];var box=surface.Bounds;
                Assert.AreEqual(box.Center,item.Transform.Position);
                Assert.AreEqual(box.HalfExtents*2,item.Transform.Scale);
                Assert.AreEqual(box.Orientation,item.Transform.Rotation);
                float floor=box.Center.Y-0.501f;
                var spawn=box.Center+Vector3.UnitX*(box.HalfExtents.X+1.5f);
                var isolated=new LevelDefinition("support-test",spawn,-100,
                    new(new(100,100,100),new(1),Quaternion.Identity),
                    [surface,new("ground",new(new(box.Center.X,floor-0.2f,box.Center.Z),new(20,0.2f,20),Quaternion.Identity))],[],[]);
                var simulator=new BallSimulator(isolated,SimulationSettings.Default);var ball=simulator.CreateInitialState();
                for(int i=0;i<180;i++)ball=simulator.Step(ball,new(-Vector2.UnitX,false,false)).Ball;
                Assert.IsGreaterThanOrEqualTo(box.Center.X+box.HalfExtents.X+0.499f,ball.Center.X,surface.Id);
                Assert.IsLessThan(0.01f,MathF.Abs(ball.Velocity.X),surface.Id);
            }
        }
        Assert.IsGreaterThan(20,tested);
    }
}