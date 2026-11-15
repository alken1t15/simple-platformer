using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Platformer.App;
using Platformer.Core;
namespace Platformer.Tests;
[TestClass]
public sealed class WinterExpansionTests
{
    [TestMethod]
    public void FestiveBulbsAnimateOnlyWithSimulationTimeAndFreezeInPause()
    {
        var host=new GameplayHost(GameLevels.CreateCatalog(),SimulationSettings.Default,new(),visual:true);
        host.Flow.Start("winter-icicle");host.Advance(new(true),0);
        var scene=host.Flow.Presenter!.Scene;
        var materials=scene.Objects.Select(o=>o.Material).Distinct().Where(m=>m.Texture is null).ToArray();
        var initial=materials.Select(m=>m.BaseColor).ToArray();
        for(int i=0;i<120;i++)host.Advance(new(true),SimulationSettings.Default.FixedStepSeconds);
        var animated=materials.Select(m=>m.BaseColor).ToArray();
        Assert.IsGreaterThanOrEqualTo(12,initial.Where((color,i)=>color!=animated[i]).Count(),"Garland bulbs must pulse");
        host.Flow.Pause();
        for(int i=0;i<120;i++)host.Advance(new(true),SimulationSettings.Default.FixedStepSeconds);
        CollectionAssert.AreEqual(animated,materials.Select(m=>m.BaseColor).ToArray());
        host.Flow.Restart();host.Advance(new(true),0);
        CollectionAssert.AreEqual(initial,materials.Select(m=>m.BaseColor).ToArray());
    }    [TestMethod]
    public void CollectiblesDoNotOverlapAndLongGapRequiresJumpPowerEvenWithShift()
    {
        foreach(var level in GameLevels.CreateCatalog().Levels)
        foreach(var power in level.Powerups)
        {
            foreach(var coin in level.Coins) Assert.IsGreaterThan(0.8f,Vector3.Distance(power.Center,coin.Center),level.Id+" "+power.Id+" "+coin.Id);
            foreach(var other in level.Powerups.Where(p=>p.Id!=power.Id)) Assert.IsGreaterThan(0.9f,Vector3.Distance(power.Center,other.Center));
        }
        var levelGap=new LevelDefinition("gap-proof",new(0,0.501f,0),-8,new(new(17,1,0),new(1),Quaternion.Identity),
            [new("takeoff",new(new(0,-0.2f,0),new(4,0.2f,3),Quaternion.Identity)),
             new("landing",new(new(15,-0.2f,0),new(4,0.2f,3),Quaternion.Identity))],[],[]);
        foreach(bool power in new[]{false,true})
        {
            var simulator=new BallSimulator(levelGap,SimulationSettings.Default);
            var ball=simulator.CreateInitialState() with { Center=new(3.7f,0.501f,0),Velocity=new(8.1f,0,0),BoostRemainingSeconds=1,JumpPowerSeconds=power?12:0 };
            bool landed=false;
            for(int i=0;i<240;i++)
            {
                ball=simulator.Step(ball,new(Vector2.UnitX,i==0,false)).Ball;
                if(ball.Grounded&&ball.Center.X>10.5f){landed=true;break;}
                if(ball.Center.Y < -8)break;
            }
            Assert.AreEqual(power,landed,"7-unit gap; Shift only must fail, powered jump must land");
        }
    }    [TestMethod]
    public void PoweredJumpAndSpeedAreStrongerAndPauseFreezesHudAndPickupState()
    {
        var level=new LevelDefinition("power-test",new(0,0.501f,0),-8,new(new(100,1,0),new(1),Quaternion.Identity),
            [new("floor",new(new(0,-0.2f,0),new(120,0.2f,5),Quaternion.Identity))],[],[],
            [new("jump",new(0,0.8f,0),PowerupKind.Jump,12),new("speed",new(0,0.8f,0),PowerupKind.Speed,12)]);
        var host=new GameplayHost(new LevelCatalog([level]),SimulationSettings.Default,new(),visual:true);
        host.Flow.Start(level.Id);host.Flow.Step(default);
        Assert.HasCount(2,host.Flow.Snapshot!.CollectedPowerupIds);
        int collectedObjects=host.Flow.Presenter!.Scene.Objects.Count;
        for(int i=0;i<120;i++)host.Flow.Step(new(Vector2.UnitX,false,false));
        Assert.AreEqual(6.75f,host.Flow.Snapshot.Ball.Velocity.X,0.01f);
        host.Flow.Step(new(Vector2.UnitX,true,false));
        Assert.IsGreaterThan(8f,host.Flow.Snapshot.Ball.Velocity.Y);
        var before=host.Flow.Snapshot;host.Flow.Pause();
        for(int i=0;i<240;i++)host.Advance(new(true),SimulationSettings.Default.FixedStepSeconds);
        Assert.AreSame(before,host.Flow.Snapshot);
        host.Flow.Continue();host.RefreshView(800,450);
        Assert.HasCount(2,host.View.Panels.Where(p=>p.Kind==MenuPanelKind.PowerFill).ToArray());
        Assert.HasCount(2,host.View.Texts.Where(t=>t.Kind==MenuTextKind.Powerup).ToArray());
        Assert.IsNotNull(MenuOverlayAdapter.Build(host.View,true));
        host.Flow.Restart();Assert.HasCount(0,host.Flow.Snapshot!.CollectedPowerupIds);
        Assert.IsGreaterThan(collectedObjects,host.Flow.Presenter!.Scene.Objects.Count);
    }    [TestMethod]
    public void IceKeepsMomentumAndPowerupsExpireAndReset()
    {
        var level = new LevelDefinition("ice-test",new(0,0.501f,0),-8,new(new(50,1,0),new(1),Quaternion.Identity),
            [new("ice",new(new(0,-0.2f,0),new(100,0.2f,5),Quaternion.Identity),traction:0.12f)],[],[],
            [new("speed",new(0,0.8f,0),PowerupKind.Speed,2),new("jump",new(0,0.8f,0),PowerupKind.Jump,2)]);
        var session=new GameSession(level,SimulationSettings.Default); session.Step(default);
        Assert.HasCount(2,session.Snapshot.CollectedPowerupIds);
        Assert.AreEqual(2f,session.Snapshot.Ball.JumpPowerSeconds);
        for(int i=0;i<120;i++)session.Step(new(Vector2.UnitX,false,false));
        float speed=session.Snapshot.Ball.Velocity.X;
        for(int i=0;i<12;i++)session.Step(default);
        Assert.IsGreaterThan(speed*0.5f,session.Snapshot.Ball.Velocity.X);
        for(int i=0;i<120;i++)session.Step(default);
        Assert.AreEqual(0f,session.Snapshot.Ball.SpeedPowerSeconds);
        Assert.HasCount(2,session.Snapshot.CollectedPowerupIds);
        session.Restart();Assert.HasCount(0,session.Snapshot.CollectedPowerupIds);Assert.AreEqual(0f,session.Snapshot.Ball.JumpPowerSeconds);
    }
    [TestMethod]
    public void PressSideContactContinuesVerticalMotionInsteadOfHovering()
    {
        var level=new LevelDefinition("press-jump",new(-1.2f,1.2f,0),-8,new(new(50,1,0),new(1),Quaternion.Identity),[],[],
            [new("press",new(0,0.7f,0),Vector3.UnitY,Vector3.Zero,new(0.6f,0.65f,2),Quaternion.Identity,0,0,HazardResponse.Fail,HazardMotionKind.Shuttle,1.8f)]);
        var session=new GameSession(level,SimulationSettings.Default,(ball,_)=>
            new(ball with{Center=ball.Center+new Vector3(0.3f,-0.05f,0),Velocity=new(2,-3,0)},
            [new(ball.Center,ball.Center+new Vector3(0.3f,-0.05f,0),0,1)]));
        session.Step(default);Assert.AreEqual(1.15f,session.Snapshot.Ball.Center.Y,0.001f);
        Assert.AreEqual(-3f,session.Snapshot.Ball.Velocity.Y);
    }
    [TestMethod]
    [DataRow("winter-crystal",false)][DataRow("winter-gifts",false)][DataRow("winter-midnight",false)]
    [DataRow("winter-crystal",true)][DataRow("winter-gifts",true)][DataRow("winter-midnight",true)]
    public void NewRoutesCompleteWithRealInput(string id,bool visual)
    {
        var host=new GameplayHost(GameLevels.CreateCatalog(),SimulationSettings.Default,new(),visual:visual);
        host.Flow.Start(id); var d=new ProductionRouteTests.Driver(host);d.Press(AppButtons.None);
        void Go(float x)
        {
            for(int i=0;i<5000 && host.Flow.Screen==AppScreen.Playing;i++)
            {
                var b=host.Flow.Snapshot!.Ball;
                if(MathF.Abs(x-b.Center.X)<0.14f && MathF.Abs(b.Center.Z)<0.14f && b.Velocity.Length()<0.5f)return;
                d.Move(new Vector2(x-b.Center.X,-b.Center.Z)*2-new Vector2(b.Velocity.X,b.Velocity.Z)*4);
            }
            Assert.AreNotEqual(AppScreen.Playing,host.Flow.Screen,"Unreachable "+x+" "+host.Flow.Snapshot!.Ball.Center);
        }
        void Hop(float edge,float landing)
        {
            Go(edge-(landing-edge>5?2:1));
            if (landing-edge>5) d.Press(AppButtons.LeftShift);
            for(int i=0;i<180 && host.Flow.Snapshot!.Ball.Center.X<edge-0.3f;i++)d.Move(Vector2.UnitX);
            d.Move(Vector2.UnitX,true);d.Go(landing,0);
            for(int i=0;i<180 && !host.Flow.Snapshot!.Ball.Grounded;i++)
            {
                var b=host.Flow.Snapshot.Ball;d.Move(new Vector2((landing-b.Center.X)*5-b.Velocity.X,0));
            }
            Assert.IsTrue(host.Flow.Snapshot!.Ball.Grounded,id+" landing");
        }
        void Cross(string name,float x)
        {
            Go(x-3);var h=GameLevels.CreateCatalog().Get(id).Hazards.Single(h=>h.Id==name);
            for(int i=0;i<1800;i++)
            {
                var s=host.Flow.Snapshot!;
                bool safe=new[]{0.2,0.5,0.8,1.1,1.4,1.7}.All(t=>HazardMotion.Evaluate(h,s.SimulationSeconds+t).Bounds.Center.Y-h.HalfExtents.Y>s.Ball.Center.Y+0.65f);
                if(safe){d.Go(x+2,0);return;}
                d.Move(new Vector2((x-3-s.Ball.Center.X)*5-s.Ball.Velocity.X,0));
            }
            Assert.Fail("No safe press phase "+name);
        }
        bool completed=false;
        for(int attempt=0;attempt<30 && !completed;attempt++)
        {
            host.Flow.Start(id); d.Press(AppButtons.None);
            try
            {
                for(int wait=0;wait<attempt*53;wait++)d.Move(Vector2.Zero);
        if(id=="winter-crystal")
        {
            Go(0);Go(7);Go(12);Cross("crystal-press",16);Go(25);Go(26.3f);
            // Балку перепрыгиваем, не обходим невидимым переносом.
            Go(32);d.Go(37,0);d.Hurdle(41);Go(44);Go(50);Go(55);Go(56.3f);Hop(59,69);Go(72);
        }
        else if(id=="winter-gifts")
        {
            Go(0);Go(3);Hop(4,10);Go(12);Hop(14,20);Go(22);Hop(24,30);Go(32);
            Cross("gift-press",40);Go(44);Go(49);Go(50.2f);Hop(52,62);Go(63);
        }
        else
        {
            Go(0);Go(8);Cross("midnight-press-a",12);Go(16);Cross("midnight-press-b",20);Go(29);Go(30.3f);
            Go(37);Go(38.2f);
            var pendulum=GameLevels.CreateCatalog().Get(id).Hazards.Single(h=>h.Id=="pendulum");
            for(int wait=0;wait<1800;wait++)
            {
                var snap=host.Flow.Snapshot!;
                if(new[]{0.2,0.4,0.6,0.8,1.0,1.2}.All(t=>MathF.Abs(HazardMotion.Evaluate(pendulum,snap.SimulationSeconds+t).Bounds.Center.Z)>1.65f))break;
                d.Move(new Vector2((38.2f-snap.Ball.Center.X)*5-snap.Ball.Velocity.X,0));
            }
            d.Go(44,0);Go(45);Go(53);Go(54.3f);d.Go(60,0);d.Hurdle(65);Go(69);Go(76);Go(77.3f);Hop(79,89);Go(90);
        }
                completed=host.Flow.Screen==AppScreen.LevelResult;
            }
            catch(AssertFailedException) when(attempt<29) { }
        }
        Assert.AreEqual(AppScreen.LevelResult,host.Flow.Screen,id);
        Assert.AreEqual(12,host.Flow.Snapshot!.CoinCount,id+" remaining:"+string.Join(',',GameLevels.CreateCatalog().Get(id).Coins.Select(c=>c.Id).Except(host.Flow.Snapshot.CollectedCoinIds)));
    }
}