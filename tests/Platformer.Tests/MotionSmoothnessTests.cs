using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Platformer.App;
using Platformer.Core;

namespace Platformer.Tests;

[TestClass]
public sealed class MotionSmoothnessTests
{
    [TestMethod]
    public void ShallowSkyStormRampKeepsGainingSpeedAfterFirstSecond()
    {
        var ramp = SkyStorm.Create().Surfaces.Single(s => s.Id == "loop-10").Bounds;
        var box = new OrientedBox(ramp.Center, new(30, ramp.HalfExtents.Y, 30), ramp.Orientation);
        var normal = Vector3.Transform(Vector3.UnitY, box.Orientation);
        var level = new LevelDefinition("long-ramp", box.ToWorld(new(0, box.HalfExtents.Y, 0)) + normal * 0.501f,
            -50, new(new(100, 100, 100), Vector3.One, Quaternion.Identity), [new("ramp", box)], [], []);
        var sim = new BallSimulator(level, SimulationSettings.Default);
        var ball = sim.CreateInitialState();
        for (int i = 0; i < 120; i++) ball = sim.Step(ball, default).Ball;
        float first = ball.Velocity.Length();
        for (int i = 0; i < 120; i++) ball = sim.Step(ball, default).Ball;
        Assert.IsGreaterThan(first + 0.4f, ball.Velocity.Length(), "Слабый склон должен разгонять шар, а не сразу гасить скорость");
    }

    [TestMethod]
    public void SkyStormReleasedBallDoesNotStopWhileSupportedByTheLoop()
    {
        var source = SkyStorm.Create();
        foreach (var ramp in source.Surfaces.Where(s => s.Id.StartsWith("loop-")).Take(29))
        {
            var box = ramp.Bounds;
            var normal = Vector3.Transform(Vector3.UnitY, box.Orientation);
            var spawn = box.ToWorld(new(0, box.HalfExtents.Y, 0)) + normal * 0.501f;
            var level = new LevelDefinition(source.Id, spawn, source.KillY, source.Finish, source.Surfaces, [], []);
            var sim = new BallSimulator(level, SimulationSettings.Default);
            var ball = sim.CreateInitialState();
            float previousSpeed = 0;
            for (int tick = 0; tick < 1200; tick++)
            {
                ball = sim.Step(ball, default).Ball;
                float speed = new Vector2(ball.Velocity.X, ball.Velocity.Z).Length();
                // После схода с петли или выхода на ровную площадку остановка нормальна.
                if (!ball.Grounded || ball.GroundNormal.Y > 0.99999f) break;
                Assert.IsGreaterThanOrEqualTo(previousSpeed > 0.15f ? 0.05f : 0, speed,
                    $"{ramp.Id} tick {tick}: speed {previousSpeed}->{speed}; grounded={ball.Grounded}; pos={ball.Center}");
                previousSpeed = speed;
            }
        }
    }

    [TestMethod]
    public void RollingIsSmoothBetweenTicksAt165HzAndHeldJumpKeepsCameraAligned()
    {
        var level = SessionFixtures.Level(finishX: 100, floorHalfWidth: 100);
        var host = new GameplayHost(new LevelCatalog([level]), SimulationSettings.Default, new());
        host.Advance(new(true), 0);
        host.Flow.Start(level.Id);
        host.Advance(new(true), 0);
        const double dt = 1d / 165;
        for (int i = 0; i < 165; i++) host.Advance(new(true, AppButtons.D), dt);
        float x = host.Flow.Presenter!.BallObject.Transform.Position.X;
        for (int i = 0; i < 100; i++)
        {
            host.Advance(new(true, AppButtons.D), dt);
            var shown = host.Flow.Presenter.BallObject.Transform.Position;
            Assert.AreEqual(SimulationSettings.Default.MoveSpeed * (float)dt, shown.X - x, 2e-5f,
                "Кадры без physics tick тоже должны показывать движение");
            x = shown.X;
        }
        for (int i = 0; i < 330; i++)
        {
            host.Advance(new(true, AppButtons.D | AppButtons.Space,
                i == 0 ? AppButtons.Space : AppButtons.None), dt);
            LevelDefinitionTests.Near(host.Flow.Presenter.BallObject.Transform.Position + new Vector3(0, 0.3f, 0), host.Camera.Target);
        }
        host.Flow.Restart();
        host.Advance(new(true), 0);
        LevelDefinitionTests.Near(host.Flow.Snapshot!.Ball.Center, host.Flow.Presenter!.BallObject.Transform.Position);
    }

    [TestMethod]
    public void CompactHudPreservesItsThreeLabelsAcrossWindowSizes()
    {
        var level = SessionFixtures.Level();
        var host = new GameplayHost(new LevelCatalog([level]), SimulationSettings.Default, new());
        host.Flow.Start(level.Id);
        foreach (var (width, height) in new[] { (800f, 450f), (1280f, 720f), (1920f, 1080f) })
        {
            host.RefreshView(width, height);
            float scale = width / 1280;
            var hud = host.View.Panels[0].Bounds;
            Assert.AreEqual(336 * scale, hud.Width);
            Assert.AreEqual(108 * scale, hud.Height);
            Assert.AreEqual("Coins 0/0", host.View.Texts[1].Text);
            Assert.AreEqual("Boost ready", host.View.Texts[2].Text);
            foreach (var text in host.View.Texts.Take(3))
                Assert.IsTrue(hud.Contains(text.Position) && text.Position.Y + text.PixelHeight <= hud.Y + hud.Height);
        }
    }

    [TestMethod]
    public void RenderInterpolationDoesNotFeedBackIntoRollingRotationOrSnapshot()
    {
        var level = SessionFixtures.Level(finishX: 100, floorHalfWidth: 100);
        var session = new GameSession(level, SimulationSettings.Default);
        var reference = new GameScenePresenter(level);
        var smooth = new GameScenePresenter(level);
        reference.Apply(session.Snapshot, GameSkins.All[0]);
        smooth.Apply(session.Snapshot, GameSkins.All[0]);
        for (int tick = 0; tick < 240; tick++)
        {
            session.Step(new(Vector2.UnitX, tick == 60, false));
            var snapshot = session.Snapshot;
            reference.Apply(snapshot, GameSkins.All[0], session.LastPath);
            smooth.Apply(snapshot, GameSkins.All[0], session.LastPath);
            Assert.AreEqual(1f, MathF.Abs(Quaternion.Dot(reference.BallObject.Transform.Rotation,
                smooth.BallObject.Transform.Rotation)), 1e-6f);
            smooth.InterpolateBall(0.3f);
            Assert.AreSame(snapshot, session.Snapshot);
        }
        smooth.ResetInterpolation();
        LevelDefinitionTests.Near(session.Snapshot.Ball.Center, smooth.BallObject.Transform.Position);
    }

    [TestMethod]
    public void CameraAndBallUseTheSameJumpFrame()
    {
        var level = SessionFixtures.Level(finishX: 100);
        var host = new GameplayHost(new LevelCatalog([level]), SimulationSettings.Default, new());
        host.Advance(new(true), 0);
        host.Flow.Start(level.Id);
        host.Advance(new(true), 0);
        host.Advance(new(true, AppButtons.Space, AppButtons.Space), 1d / 60);
        var ball = host.Flow.Presenter!.BallObject.Transform.Position;
        Assert.AreEqual(ball.Y + 0.3f, host.Camera.Target.Y, 1e-5f,
            "Камера и шар должны использовать одно визуальное положение");
    }
}
