using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Platformer.App;
using Platformer.Core;

namespace Platformer.Tests;

[TestClass]
public sealed class ProductionRouteTests
{
    // Направление задаётся только WASD + mouse yaw; нет телепортации/подмены физики.
    internal sealed class Driver
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

        public void Hurdle(float x, float z = 0)
        {
            Go(x - 2, z);
            for (int i = 0; i < 240 && Host.Flow.Snapshot!.Ball.Center.X < x - 1.45f; i++) Move(Vector2.UnitX);
            Move(Vector2.UnitX, jump: true);
            for (int i = 0; i < 240 && Host.Flow.Snapshot!.Ball.Center.X < x + 1.6f; i++) Move(Vector2.UnitX);
        }

        public void Gap(float edge, float landing)
        {
            Go(edge - 0.8f, 8);
            for (int i = 0; i < 180 && Host.Flow.Snapshot!.Ball.Center.X < edge - 0.35f; i++) Move(Vector2.UnitX);
            Assert.IsTrue(Host.Flow.Snapshot!.Ball.Grounded, $"Takeoff {edge}: {Host.Flow.Snapshot.Ball.Center}");
            Move(Vector2.UnitX, true);
            Go(landing, 8);
            for (int i = 0; i < 120 && !Host.Flow.Snapshot!.Ball.Grounded; i++)
            {
                var ball = Host.Flow.Snapshot!.Ball;
                Move(new Vector2(landing - ball.Center.X, 8 - ball.Center.Z) * 5 - new Vector2(ball.Velocity.X, ball.Velocity.Z));
            }
            Assert.IsTrue(Host.Flow.Snapshot!.Ball.Grounded);
        }

        public void SkyStorm()
        {
            Go(-6, 0); Go(8, 0);
            for (int i = 1; i <= 36; i++)
            {
                float angle = -MathF.PI / 2 + i * MathF.PI / 24;
                Go(8 + 8 * MathF.Cos(angle), 8 + 8 * MathF.Sin(angle));
            }
            Go(22, 8);
            Gap(24, 26); Gap(27, 29); Gap(30, 32); Gap(33, 35);
            Hurdle(38, 8);
            Go(39.7f, 10.3f); Go(46, 10.3f); Go(48, 8); Go(51, 8);
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ProductionCatalogPassesAllThirteenLocationsThroughInputRunnerSessionPresenterAndAudio(bool visual)
    {
        var playback = new AudioCoordinatorTests.Playback();
        using var audio = new AudioCoordinator(playback);
        var host = new GameplayHost(GameLevels.CreateCatalog(), SimulationSettings.Default,
            new UserSettings { SkinId = "football", MasterVolume = 0.4f }, audio: audio, visual: visual);
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
        driver.Go(7, 16);
        driver.Go(-5, 16);
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
        driver.Go(35, 8);
        driver.Go(39, 8);
        Assert.AreEqual(AppScreen.LevelResult, host.Flow.Screen);
        Assert.AreEqual(15, host.Flow.Snapshot!.CoinCount);
        driver.Press(AppButtons.Enter);
        Assert.AreEqual("jump-circuit", host.Flow.Snapshot!.LevelId);
        driver.Hurdle(5.5f);
        driver.Hurdle(10);
        driver.Hurdle(14.5f);
        driver.Go(20, 0);
        driver.Go(20, 10);
        Assert.AreEqual(AppScreen.LevelResult, host.Flow.Screen);
        Assert.AreEqual(10, host.Flow.Snapshot!.CoinCount);
        driver.Press(AppButtons.Enter);
        Assert.AreEqual("island-circuit", host.Flow.Snapshot!.LevelId);
        for (int i = 1; i <= 12; i++)
        {
            float angle = -MathF.PI / 2 + i * MathF.PI / 24;
            driver.Go(7 * MathF.Cos(angle), 7 + 7 * MathF.Sin(angle));
        }
        // Увеличенная балка требует обхода по краю арены. Два прыжка
        // соединяют рампу/дугу с этим краем, не проходя сквозь саму балку.
        driver.Go(7, 11.1f);
        driver.Move(Vector2.UnitY, jump: true);
        driver.Go(9.75f, 11.8f);
        driver.Go(9.75f, 16.2f);
        driver.Move(-Vector2.UnitX, jump: true);
        for (int i = 3; i <= 12; i++)
        {
            float angle = MathF.PI - i * MathF.PI / 24;
            driver.Go(14 + 7 * MathF.Cos(angle), 14 + 7 * MathF.Sin(angle));
        }
        driver.Go(14.8f, 21);
        for (int i = 0; i < 200 && host.Flow.Snapshot!.Ball.Center.X < 15.65f; i++) driver.Move(Vector2.UnitX);
        driver.Move(Vector2.UnitX, jump: true);
        driver.Go(20, 21);
        driver.Go(24, 21);
        Assert.AreEqual(AppScreen.LevelResult, host.Flow.Screen);
        Assert.AreEqual(11, host.Flow.Snapshot!.CoinCount);
        Assert.AreEqual(12, host.Flow.Progress.BaseStars);
        driver.Press(AppButtons.Enter);
        foreach (string challenge in ChallengeRoutes.Ids.Concat(ChallengeRoutes.ExpertIds))
        {
            Assert.AreEqual(challenge, host.Flow.Snapshot!.LevelId);
            ChallengeRoutes.Run(driver, challenge);
            Assert.AreEqual(AppScreen.LevelResult, host.Flow.Screen, challenge);
            Assert.AreEqual(12, host.Flow.Snapshot!.CoinCount, challenge);
            driver.Press(AppButtons.Enter);
        }
        Assert.AreEqual(GameLevels.FinalLevelId, host.Flow.Snapshot!.LevelId);
        driver.SkyStorm();
        Assert.AreEqual(AppScreen.LevelResult, host.Flow.Screen);
        Assert.AreEqual(20, host.Flow.Snapshot!.CoinCount);
        driver.Press(AppButtons.Enter);
        Assert.AreEqual(AppScreen.GameComplete, host.Flow.Screen);
        Assert.AreEqual(162, host.Flow.TotalCoinCount);
        Assert.AreEqual(39, host.Flow.Progress.TotalStars);
        Assert.IsTrue(host.Flow.Progress.IsSkinUnlocked("champion"));
        Assert.AreEqual("football", host.Flow.UserSettings.SkinId);
        Assert.AreEqual(0.4f, host.Flow.UserSettings.MasterVolume);
        Assert.AreEqual(162, playback.Effects.Count(item => item.Kind == GameEventKind.CoinCollected));
        Assert.AreEqual(13, playback.Effects.Count(item => item.Kind == GameEventKind.LevelCompleted));
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
        Click("levels"); Click("level:level2");
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
