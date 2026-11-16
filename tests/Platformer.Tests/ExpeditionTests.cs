using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Platformer.App;
using Platformer.Core;

namespace Platformer.Tests;

[TestClass]
public sealed class ExpeditionTests
{
    [TestMethod]
    public void WinterCoinsStayClearOfTrapStructuresAndTheWholeMotionCycle()
    {
        var overlaps=new HashSet<string>();
        foreach(var level in GameLevels.CreateCatalog().Levels.Where(l=>GameChapters.For(l.Id)==GameChapter.Winter))
        foreach(var coin in level.Coins)
        {
            foreach(var surface in level.Surfaces.Where(s=>s.IsBarrier))
                if(Vector3.Distance(coin.Center,surface.Bounds.ClosestPoint(coin.Center))<=coin.Radius+.25f)
                    overlaps.Add(level.Id+" "+coin.Id+" / "+surface.Id);
            foreach(var hazard in level.Hazards)
            {
            // Расстояние меняется не быстрее самой быстрой точки ловушки.
            // Запас .25 перекрывает движение от любого момента до ближайшего сэмпла.
            double period=Math.Tau/Math.Max(.01f,MathF.Abs(hazard.AngularSpeed));
            Assert.IsLessThan(.25, HazardMotion.MaximumPointSpeed(hazard)*period/256,level.Id+" sampling margin");
            for(int phase=0;phase<128;phase++)
            {
                double seconds=phase*period/128;
                var bounds=HazardMotion.Evaluate(hazard,seconds).Bounds;
                if(Vector3.Distance(coin.Center,bounds.ClosestPoint(coin.Center))<=coin.Radius+.25f)
                    overlaps.Add(level.Id+" "+coin.Id+" / "+hazard.Id);
            }
            }
        }
        Assert.IsEmpty(overlaps,string.Join("\n",overlaps));
    }

    [TestMethod]
    public void EachLongRouteContainsPressesSweepersAndAnAnchoredWindMarker()
    {
        foreach(var level in GameLevels.CreateCatalog().Levels.Where(l=>l.Id is "winter-spring-trail" or "winter-glacier-ferries" or "winter-windward"))
        {
            Assert.HasCount(4,level.Hazards);
            Assert.AreEqual(2,level.Hazards.Count(h=>h.Response==HazardResponse.Fail));
            Assert.AreEqual(2,level.Hazards.Count(h=>h.Response==HazardResponse.Knockback));
            Assert.IsNotEmpty(level.Winds);
            var presenter=new GameScenePresenter(level,true);
            Assert.IsFalse(presenter.Scene.Objects.Any(o=>o.Transform.Scale==new Vector3(.035f,.035f,.7f)),"Floating wind sticks must not return");
            var poles=presenter.Scene.Objects.Where(o=>o.Transform.Scale==new Vector3(.055f,1.6f,.055f)).ToArray();
            Assert.HasCount(level.Winds.Count,poles);
            foreach(var pole in poles)
            {
                var foot=pole.Transform.Position-Vector3.UnitY*.8f;
                Assert.IsTrue(level.Surfaces.Where(s=>!s.IsBarrier).Any(s=>Vector3.Distance(s.Bounds.ClosestPoint(foot),foot)<.01f));
            }
        }
    }

    [TestMethod]
    public void PlatformTransportStopsAtWallsAndPauseRestartKeepItsClock()
    {
        var deck=new SurfaceDefinition("deck",new(new(0,-.2f,0),new(2,.2f,2),Quaternion.Identity),motion:new(new(4,0,0),20));
        var wall=new SurfaceDefinition("wall",new(new(2,1,0),new(.2f,1,3),Quaternion.Identity),isBarrier:true);
        var level=new LevelDefinition("test",new(0,.501f,0),-10,new(new(40,1,0),Vector3.One,Quaternion.Identity),[deck,wall],[],[]);
        var run=new GameSession(level,SimulationSettings.Default);
        for(int i=0;i<360;i++)run.Step(default);
        Assert.IsLessThanOrEqualTo(1.301f,run.Snapshot.Ball.Center.X,"Carrier pushed the ball through a wall");
        var host=new GameplayHost(GameLevels.CreateCatalog(),SimulationSettings.Default,new());
        host.Flow.Start("winter-glacier-ferries");host.Advance(new(true),0);
        for(int i=0;i<60;i++)host.Advance(new(true),SimulationSettings.Default.FixedStepSeconds);
        var ferry=host.Flow.Presenter!.SurfaceObjects["ferry-one"];
        var position=ferry.Transform.Position;
        host.Flow.Pause();for(int i=0;i<60;i++)host.Advance(new(true),SimulationSettings.Default.FixedStepSeconds);
        Assert.AreEqual(position,ferry.Transform.Position);
        host.Flow.Restart();Assert.AreEqual(30,ferry.Transform.Position.X);
    }

    [TestMethod]
    public void MusicMixRestartsOnlyOnChangeAndNeverStartsANewVoiceInPause()
    {
        using var backend=new MixProbe();using var coordinator=new AudioCoordinator(backend);
        var level=GameLevels.CreateCatalog().Levels[0];var settings=new UserSettings();
        coordinator.SetScreen(AppScreen.Playing);Assert.AreEqual(1,backend.MusicStarts);
        coordinator.UpdateSoundscape(level,null,AppScreen.Playing,settings);
        coordinator.SetScreen(AppScreen.Playing);Assert.AreEqual(1,backend.MusicStarts);
        coordinator.SetScreen(AppScreen.Paused);settings.MusicVolume=.4f;
        coordinator.UpdateSoundscape(level,null,AppScreen.Paused,settings);coordinator.SetScreen(AppScreen.Paused);
        Assert.AreEqual(1,backend.MusicStarts);
        coordinator.SetScreen(AppScreen.Playing);Assert.AreEqual(2,backend.MusicStarts);
        settings.EffectsVolume=.3f;coordinator.UpdateSoundscape(level,null,AppScreen.Playing,settings);
        coordinator.SetScreen(AppScreen.Playing);Assert.AreEqual(2,backend.MusicStarts);
    }
    private sealed class MixProbe:IGameAudioPlayback,IGameSoundscape
    {
        private float gain=1;public int MusicStarts {get;private set;}
        public bool SetMix(float music,float effects){bool changed=gain!=music;gain=music;return changed;}
        public void UpdateSoundscape(LevelDefinition l,SessionSnapshot? s,AppScreen screen,UserSettings settings){}
        public void PauseSoundscape(){}
        public IGameAudioVoice PlayMusic(string trackId=MusicTracks.OriginalId){MusicStarts++;return new Voice();}
        public IGameAudioVoice PlayEffect(GameEventKind kind)=>new Voice();
        public void Dispose(){}
        private sealed class Voice:IGameAudioVoice{public void Pause(){}public void Resume(){}public void Dispose(){}}
    }

    [TestMethod]
    public void DisabledFlashingKeepsWinterLightsSteadyAndReducedEffectsThinTheSnow()
    {
        var user=new UserSettings{FlashingEnabled=false,ReducedEffects=true};
        var host=new GameplayHost(GameLevels.CreateCatalog(),SimulationSettings.Default,user,visual:true);
        host.Flow.Start("winter-snowbound");host.Advance(new(true),0);
        var lights=host.Flow.Presenter!.Scene.Objects.Where(o=>o.Transform.Scale==new Vector3(.075f)).Select(o=>o.Material).ToArray();
        var colors=lights.Select(m=>m.BaseColor).ToArray();
        for(int i=0;i<120;i++)host.Advance(new(true),SimulationSettings.Default.FixedStepSeconds);
        CollectionAssert.AreEqual(colors,lights.Select(m=>m.BaseColor).ToArray());
        Assert.IsGreaterThanOrEqualTo(50,host.Flow.Presenter.Scene.Objects.Count(o=>o.Transform.Scale==new Vector3(.001f)));
    }

    [TestMethod]
    [DataRow("winter-spring-trail")]
    [DataRow("winter-glacier-ferries")]
    [DataRow("winter-windward")]
    public void LongExpeditionRoutesFinishWithPhysicsCommands(string id)
    {
        var level=GameLevels.CreateCatalog().Get(id);
        var run=new GameSession(level,SimulationSettings.Default);
        int ferryIndex=0,state=0;
        var crossed=new HashSet<string>();
        var trace=new Queue<string>();
        var ferries=level.Surfaces.Where(s=>s.Motion is not null).OrderBy(s=>s.Bounds.Center.X).ToArray();
        for(int tick=0;tick<65000&&run.Snapshot.Phase==SessionPhase.Running;tick++)
        {
            var snapshot=run.Snapshot;var ball=snapshot.Ball;
            if(tick%30==0){trace.Enqueue($"t{tick} s{state} p{ball.Center} v{ball.Velocity} g{ball.Grounded}");if(trace.Count>20)trace.Dequeue();}
            float target=level.Finish.Center.X;bool jump=false;
            if(ferryIndex<ferries.Length)
            {
                var ferry=ferries[ferryIndex];var bounds=ferry.BoundsAt(snapshot.SimulationSeconds);
                var left=level.Surfaces.Where(s=>!s.IsBarrier&&s.Motion is null&&s.Bounds.Center.X<ferry.Bounds.Center.X)
                    .Max(s=>s.Bounds.Center.X+s.Bounds.HalfExtents.X);
                var right=level.Surfaces.Where(s=>!s.IsBarrier&&s.Motion is null&&s.Bounds.Center.X>ferry.Bounds.Center.X)
                    .Min(s=>s.Bounds.Center.X-s.Bounds.HalfExtents.X);
                if(ball.Center.X>right+0.3f&&ball.Grounded){ferryIndex++;state=0;}
                else if(state==0)
                {
                    target=left-0.55f;
                    if(ball.Center.X>left-0.9f&&bounds.Center.X-bounds.HalfExtents.X<left+1.4f)
                    {state=1;jump=true;target=bounds.Center.X;}
                }
                else if(state==1)
                {
                    target=bounds.Center.X;
                    if(ball.Grounded&&ball.Center.X>left+0.3f)state=2;
                }
                else if(state==2)
                {
                    target=bounds.Center.X;
                    if(bounds.Center.X+bounds.HalfExtents.X>right-1.8f)state=3;
                }
                else if(state==3)
                {
                    target=right+1;
                    if(ball.Center.X>bounds.Center.X+bounds.HalfExtents.X-0.65f&&ball.Grounded){jump=true;state=4;}
                }
                else target=right+1;
            }
            if(id=="winter-spring-trail"&&ball.Center.X>131.1f&&ball.Center.X<132.3f&&ball.Grounded)jump=true;
            if(id=="winter-windward"&&ball.Grounded&&((ball.Center.X>87.1f&&ball.Center.X<88.3f)
                ||(ball.Center.X>101.1f&&ball.Center.X<102.3f)))jump=true;
            foreach(var hazard in level.Hazards)
            {
                if(ball.Center.X>hazard.Pivot.X+3)continue;
                if(hazard.MotionKind==HazardMotionKind.Shuttle&&ball.Center.X>hazard.Pivot.X-4
                    &&ball.Center.X<hazard.Pivot.X-1.5f&&!crossed.Contains(hazard.Id))
                {
                    bool safe=new[]{.2,.45,.7,.95}.All(offset=>
                    {
                        var box=HazardMotion.Evaluate(hazard,snapshot.SimulationSeconds+offset).Bounds;
                        return box.Center.Y-box.HalfExtents.Y>hazard.Pivot.Y-2.5f+1.2f;
                    });
                    if(safe)crossed.Add(hazard.Id);else target=MathF.Min(target,hazard.Pivot.X-2.4f);
                }
                if(hazard.MotionKind==HazardMotionKind.Rotation&&ball.Grounded
                    &&ball.Center.X>hazard.Pivot.X-1.9f&&ball.Center.X<hazard.Pivot.X-.5f)jump=true;
            }
            float x=Math.Clamp((target-ball.Center.X)*3-ball.Velocity.X*0.6f,-1,1);
            float z=Math.Clamp(-ball.Center.Z*4-ball.Velocity.Z*0.7f,-1,1);
            var move=new Vector2(x,z);if(move.Length()>1)move=Vector2.Normalize(move);
            run.Step(new(move,jump,false));
        }
        Assert.AreEqual(SessionPhase.LevelComplete,run.Snapshot.Phase,
            id+" x="+run.Snapshot.Ball.Center+" ferry="+ferryIndex+" state="+state+"\n"+string.Join("\n",trace));
        Assert.AreEqual(level.Coins.Count,run.Snapshot.CoinCount,
            string.Join(",",level.Coins.Select(c=>c.Id).Except(run.Snapshot.CollectedCoinIds)));
    }

    [TestMethod]
    public void MovingDeckCarriesAnIdleBallWithoutRecreatingTheGameSession()
    {
        var surface=new SurfaceDefinition("ferry",new(new(0,-0.2f,0),new(2,0.2f,2),Quaternion.Identity),motion:new(new(4,0,0),20));
        var level=new LevelDefinition("test",new(0,0.501f,0),-10,new(new(40,1,0),Vector3.One,Quaternion.Identity),[surface],[],[]);
        var session=new GameSession(level,SimulationSettings.Default);var run=session.Snapshot.SessionId;
        for(int i=0;i<600;i++)session.Step(default);
        Assert.AreEqual(run,session.Snapshot.SessionId);Assert.IsTrue(session.Snapshot.Ball.Grounded);
        Assert.AreEqual(surface.Motion!.Offset(session.Snapshot.SimulationSeconds).X,session.Snapshot.Ball.Center.X,0.02f);
        Assert.AreEqual(0.501f,session.Snapshot.Ball.Center.Y,0.01f);
        var presenter=new GameScenePresenter(level);presenter.Apply(session.Snapshot,GameSkins.All[0]);
        Assert.AreEqual(surface.BoundsAt(session.Snapshot.SimulationSeconds).Center,presenter.SurfaceObjects["ferry"].Transform.Position);
    }

    [TestMethod]
    public void SpringsLaunchAutomaticallyAndWindOnlyActsInsideItsVolume()
    {
        var floor=new SurfaceDefinition("floor",new(new(0,-0.2f,0),new(5,0.2f,5),Quaternion.Identity));
        var spring=new SpringPad("spring",new(new(0,-0.04f,0),new(1,0.04f,1),Quaternion.Identity));
        var level=new LevelDefinition("test",new(0,0.501f,0),-10,new(new(40,1,0),Vector3.One,Quaternion.Identity),[floor],[],[],springs:[spring]);
        var session=new GameSession(level,SimulationSettings.Default);session.Step(default);
        Assert.IsGreaterThan(10f,session.Snapshot.Ball.Velocity.Y);Assert.IsFalse(session.Snapshot.Ball.Grounded);
        var wind=new WindZone("wind",new(new(0,4,0),new(8),Quaternion.Identity),new(0,0,3));
        var windy=new LevelDefinition("air",new(0,6,0),-20,level.Finish,[],[],[],winds:[wind]);
        var air=new GameSession(windy,SimulationSettings.Default);
        for(int i=0;i<60;i++)air.Step(default);
        Assert.IsGreaterThan(1.4f,air.Snapshot.Ball.Velocity.Z);
        var calm=new GameSession(new("calm",new(20,6,0),-20,level.Finish,[],[],[],winds:[wind]),SimulationSettings.Default);
        for(int i=0;i<60;i++)calm.Step(default);
        Assert.AreEqual(0,calm.Snapshot.Ball.Velocity.Z);
    }

    [TestMethod]
    public void ExpeditionSettingsRoundTripAndLegacyFilesUseSafeDefaults()
    {
        string path=Path.Combine(Path.GetTempPath(),Guid.NewGuid()+".json");
        try
        {
            var store=new SettingsStore(path);var progress=new PlayerProgress(GameLevels.CreateCatalog());
            File.WriteAllText(path,"{\"Version\":1}");
            Assert.AreEqual(1,store.Load(progress).MusicVolume);Assert.IsTrue(store.Load(progress).FlashingEnabled);
            var stored=StoredSettings.Default with{MusicVolume=.3f,EffectsVolume=.6f,ReducedEffects=true,
                FlashingEnabled=false,Bindings=new ControlBindings().Cycle(GameAction.Jump,1)};
            Assert.IsTrue(store.Save(stored));Assert.AreEqual(stored,store.Load(progress));
            File.WriteAllText(path,"{\"Version\":1,\"MusicVolume\":\"bad\",\"Bindings\":{\"Jump\":999}}");
            Assert.AreEqual(1,store.Load(progress).MusicVolume);Assert.IsTrue(store.Load(progress).Bindings.IsValid);
        }
        finally{File.Delete(path);}
    }

    [TestMethod]
    public void ExpeditionMenusKeepThumbnailsAndExtraSettingsInsideTheWindow()
    {
        var catalog=GameLevels.CreateCatalog();var host=new GameplayHost(catalog,SimulationSettings.Default,new());
        host.Flow.OpenSettings();host.RefreshView(1280,720);
        void Click(string id)
        {
            host.RefreshView(1280,720);var r=host.View.Controls.Single(c=>c.Id==id).Bounds;
            host.Advance(new(true,PointerPosition:new(r.X+r.Width/2,r.Y+r.Height/2)),0,1280,720);
            host.Advance(new(true,AppButtons.PointerLeft,AppButtons.PointerLeft,PointerPosition:new(r.X+r.Width/2,r.Y+r.Height/2)),0,1280,720);
            host.Advance(new(true),0,1280,720);
        }
        Click("more-settings");
        foreach(var size in new[]{(800f,450f),(1280f,720f)})
        {host.RefreshView(size.Item1,size.Item2);Assert.IsTrue(host.View.Controls.All(c=>c.Bounds.Y+c.Bounds.Height<=size.Item2));}
        Click("reduced-effects");Assert.IsTrue(host.Flow.UserSettings.ReducedEffects);
        Click("flashing");Assert.IsFalse(host.Flow.UserSettings.FlashingEnabled);
        Click("bindings");var original=host.Flow.UserSettings.Bindings.Jump;
        Click("bind:Jump");Assert.AreNotEqual(original,host.Flow.UserSettings.Bindings.Jump);
        Assert.IsTrue(host.Flow.UserSettings.Bindings.IsValid);
    }
}
