using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Platformer.App;
using Platformer.Core;

namespace Platformer.Tests;

[TestClass]
public sealed class ProductionRouteTests
{
    // Направление задаётся только WASD + mouse yaw; нет телепортации/подмены физики.
    private sealed class Driver
    {
        public GameplayHost Host { get; }
        public Driver(GameplayHost host) => Host = host;
        public void Press(AppButtons button)
        {
            Host.Advance(new(true), 0);
            Host.Advance(new(true, button, button), 0);
            Host.Advance(new(true), 0);
        }
        public void Move(Vector2 direction, bool jump = false)
        {
            float yaw = MathF.Atan2(-direction.X, -direction.Y);
            float delta = (float)Math.IEEERemainder(yaw - Host.Frames.Camera.YawRadians, Math.Tau);
            var down = direction.LengthSquared() > 0.01f ? AppButtons.W : AppButtons.None;
            Host.Advance(new(true, down | (jump ? AppButtons.Space : AppButtons.None),
                jump ? AppButtons.Space : AppButtons.None, new(-delta / CameraRig.MouseSensitivity, 0)),
                SimulationSettings.Default.FixedStepSeconds);
            Assert.AreNotEqual(AppScreen.Failed, Host.Flow.Screen, $"Failed: {Host.Flow.Snapshot?.Ball.Center}");
        }
        public void Go(float x, float z)
        {
            for (int i = 0; i < 1600 && Host.Flow.Screen == AppScreen.Playing; i++)
            {
                var ball = Host.Flow.Snapshot!.Ball;
                var delta = new Vector2(x - ball.Center.X, z - ball.Center.Z);
                if (delta.Length() < 0.14f) return;
                Move(delta * 5f - new Vector2(ball.Velocity.X, ball.Velocity.Z));
            }
            Assert.AreNotEqual(AppScreen.Playing, Host.Flow.Screen, $"Unreachable {x},{z}: {Host.Flow.Snapshot?.Ball.Center}");
        }
    }

    [TestMethod]
    public void ProductionCatalogPassesBothLocationsThroughInputRunnerSessionPresenterAndAudio()
    {
        var playback = new AudioCoordinatorTests.Playback();
        using var audio = new AudioCoordinator(playback);
        var host = new GameplayHost(GameLevels.CreateCatalog(), SimulationSettings.Default,
            new UserSettings { SkinId = "orange", MasterVolume = 0.4f }, audio: audio);
        var driver = new Driver(host);
        driver.Press(AppButtons.Enter);
        driver.Go(-0.3f, -0.65f);
        driver.Go(0.9f, 0.5f);
        driver.Go(2f, 0);
        driver.Go(7, 0);
        driver.Go(7, 11.5f);
        for (int i = 0; i < 200 && host.Flow.Snapshot!.Ball.Center.Z < 11.65f; i++) driver.Move(Vector2.UnitY);
        Assert.IsGreaterThanOrEqualTo(11.65f, host.Flow.Snapshot!.Ball.Center.Z);
        driver.Move(Vector2.UnitY, true);
        driver.Go(7, 15);
        driver.Go(-5, 15);
        Assert.AreEqual(AppScreen.LevelResult, host.Flow.Screen);
        Assert.AreEqual(10, host.Flow.Snapshot!.CoinCount);
        driver.Press(AppButtons.Enter); // Next из настоящего MenuController
        Assert.AreEqual("level2", host.Flow.Snapshot!.LevelId);
        driver.Go(10, 0);
        driver.Go(10, -6);
        driver.Go(10, 0);
        driver.Go(10, 8);
        driver.Go(16.7f, 8);
        driver.Go(16.7f, 10.9f);
        driver.Go(23.3f, 10.9f);
        driver.Go(23.3f, 8);
        driver.Go(29.8f, 8);
        for (int i = 0; i < 200 && host.Flow.Snapshot!.Ball.Center.X < 30.65f; i++) driver.Move(Vector2.UnitX);
        Assert.IsGreaterThanOrEqualTo(30.65f, host.Flow.Snapshot!.Ball.Center.X);
        driver.Move(Vector2.UnitX, true);
        driver.Go(34, 8);
        driver.Go(38, 8);
        Assert.AreEqual(AppScreen.LevelResult, host.Flow.Screen);
        Assert.AreEqual(15, host.Flow.Snapshot!.CoinCount);
        driver.Press(AppButtons.Enter);
        Assert.AreEqual(AppScreen.GameComplete, host.Flow.Screen);
        Assert.AreEqual(25, host.Flow.TotalCoinCount);
        Assert.AreEqual("orange", host.Flow.UserSettings.SkinId);
        Assert.AreEqual(0.4f, host.Flow.UserSettings.MasterVolume);
        Assert.AreEqual(25, playback.Effects.Count(item => item.Kind == GameEventKind.CoinCollected));
        Assert.AreEqual(2, playback.Effects.Count(item => item.Kind == GameEventKind.LevelCompleted));
        Assert.IsTrue(playback.Music.All(voice => voice.Disposed));
    }

    [TestMethod]
    public void DirectSecondLevelRetryRestoresCoinsHazardAndAudioWithoutInventingFirstLevelScore()
    {
        var playback = new AudioCoordinatorTests.Playback();
        using var audio = new AudioCoordinator(playback);
        var host = new GameplayHost(GameLevels.CreateCatalog(), SimulationSettings.Default, new UserSettings(), audio: audio);
        var driver = new Driver(host);
        void Click(string id)
        {
            host.Advance(new(true), 0);
            var bounds = host.View.Controls.Single(control => control.Id == id).Bounds;
            host.Advance(new(true, AppButtons.PointerLeft, AppButtons.PointerLeft,
                PointerPosition: new(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2)), 0);
            host.Advance(new(true), 0);
        }
        Click("levels"); Click("level:level2"); Click("start");
        var initial = host.Flow.Snapshot!;
        Assert.AreEqual("level2", initial.LevelId);
        driver.Go(10, 0);
        Assert.IsGreaterThan(0, host.Flow.Snapshot!.CoinCount);
        Assert.IsEmpty(host.Flow.CompletedLevels);
        driver.Press(AppButtons.Escape);
        Assert.IsTrue(playback.Music.Single().Paused);
        Click("restart");
        var restarted = host.Flow.Snapshot!;
        Assert.AreNotEqual(initial.SessionId, restarted.SessionId);
        Assert.AreEqual(initial.Ball.Center, restarted.Ball.Center);
        Assert.AreEqual(initial.Hazards.Single().Bounds.Center, restarted.Hazards.Single().Bounds.Center);
        Assert.AreEqual(initial.Hazards.Single().Bounds.Orientation, restarted.Hazards.Single().Bounds.Orientation);
        Assert.AreEqual(initial.Hazards.Single().Bounds.HalfExtents, restarted.Hazards.Single().Bounds.HalfExtents);
        Assert.AreEqual(0, restarted.CoinCount);
        Assert.IsTrue(playback.Music[0].Disposed);
        for (int i = 0; i < 400 && host.Flow.Screen == AppScreen.Playing; i++)
            host.Advance(new(true, AppButtons.W), SimulationSettings.Default.FixedStepSeconds);
        Assert.AreEqual(AppScreen.Failed, host.Flow.Screen);
        Assert.AreEqual(1, playback.Effects.Count(item => item.Kind == GameEventKind.Failed));
        driver.Press(AppButtons.R);
        Assert.AreEqual(AppScreen.Playing, host.Flow.Screen);
        Assert.AreEqual(0, host.Flow.TotalCoinCount);
        Assert.AreEqual(initial.Ball.Center, host.Flow.Snapshot!.Ball.Center);
        Assert.IsTrue(playback.Effects.All(item => item.Voice.Disposed));
    }
}
