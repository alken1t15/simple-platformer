using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Platformer.App;
using Platformer.Core;

namespace Platformer.Tests;

[TestClass]
public sealed class UsabilityFeedbackTests
{
    [TestMethod]
    public void JumpKeepsSpinningWhileCameraTurnsAndPauseStopsVisualTime()
    {
        var level = SessionFixtures.Level(finishX: 100f, floorHalfWidth: 100f);
        var host = new GameplayHost(new LevelCatalog([level]), SimulationSettings.Default, new());
        host.Flow.Start(level.Id);
        host.Advance(new(true), 0);
        for (int i = 0; i < 20; i++) host.Advance(new(true, AppButtons.D), 0.02);
        Assert.IsTrue(host.Flow.Snapshot!.Ball.Grounded);
        Assert.IsGreaterThan(0.1f, host.Flow.Snapshot.Ball.Velocity.X);
        host.Advance(new(true, AppButtons.D | AppButtons.Space, AppButtons.Space), 0.02);
        for (int i = 0; i < 6; i++)
        {
            var rotation = host.Flow.Presenter!.BallObject.Transform.Rotation;
            float yaw = host.Frames.Camera.YawRadians;
            host.Advance(new(true, AppButtons.D, MouseDelta: new(20, 0)), 0.02);
            Assert.IsFalse(host.Flow.Snapshot.Ball.Grounded);
            Assert.AreNotEqual(yaw, host.Frames.Camera.YawRadians);
            Assert.IsLessThan(0.99999f, MathF.Abs(Quaternion.Dot(rotation, host.Flow.Presenter.BallObject.Transform.Rotation)));
        }
        host.Advance(new(true, AppButtons.Escape, AppButtons.Escape), 0.02);
        Assert.AreEqual(AppScreen.Paused, host.Flow.Screen);
        var paused = host.Flow.Presenter!.BallObject.Transform.Rotation;
        for (int i = 0; i < 10; i++) host.Advance(new(true, MouseDelta: new(30, 20)), 0.1);
        Assert.AreEqual(paused, host.Flow.Presenter.BallObject.Transform.Rotation);
        host.Flow.Continue();
        host.Advance(new(true), 0);
        host.Advance(new(true), 0.02);
        Assert.IsLessThan(0.99999f, MathF.Abs(Quaternion.Dot(paused, host.Flow.Presenter.BallObject.Transform.Rotation)));
        host.Flow.Restart();
        Assert.AreEqual(Quaternion.Identity, host.Flow.Presenter.BallObject.Transform.Rotation);
    }

    [TestMethod]
    public void LandingAndNewSessionClearSpinAndInvalidTimeDoesNotMutateScene()
    {
        var level = TrainingTrack.Create();
        var presenter = new GameScenePresenter(level);
        Guid id = Guid.NewGuid();
        SessionSnapshot Snapshot(long tick, double seconds, bool grounded, Vector3 velocity) =>
            new(id, level.Id, tick, seconds, SessionPhase.Running,
                new(new((float)seconds, 1, 0), velocity, 0.5f, grounded, grounded ? Vector3.UnitY : Vector3.Zero, 0, 0), [], []);
        presenter.Apply(Snapshot(0, 0, true, Vector3.UnitX), GameSkins.All[0]);
        presenter.Apply(Snapshot(1, 0.1, false, Vector3.UnitX), GameSkins.All[0]);
        presenter.Apply(Snapshot(2, 0.2, true, Vector3.Zero), GameSkins.All[0]);
        var landed = presenter.BallObject.Transform.Rotation;
        presenter.Apply(Snapshot(3, 0.3, false, Vector3.UnitX), GameSkins.All[0]);
        Assert.AreEqual(landed, presenter.BallObject.Transform.Rotation);
        var position = presenter.BallObject.Transform.Position;
        Assert.ThrowsExactly<ArgumentException>(() => presenter.Apply(Snapshot(4, 0.25, false, Vector3.Zero), GameSkins.All[0]));
        Assert.ThrowsExactly<ArgumentException>(() => presenter.Apply(Snapshot(3, 0.30001, false, Vector3.UnitX), GameSkins.All[0]));
        Assert.AreEqual(position, presenter.BallObject.Transform.Position);
        Assert.AreEqual(landed, presenter.BallObject.Transform.Rotation);
        id = Guid.NewGuid();
        presenter.Apply(Snapshot(0, 0, true, Vector3.Zero), GameSkins.All[0]);
        presenter.Apply(Snapshot(1, 0.1, false, Vector3.UnitX), GameSkins.All[0]);
        Assert.AreEqual(Quaternion.Identity, presenter.BallObject.Transform.Rotation);
    }

    [TestMethod]
    public void FiniteExtremeVelocityAndLongFlightStillProduceNormalizedRotation()
    {
        var level = TrainingTrack.Create();
        var presenter = new GameScenePresenter(level);
        Guid id = Guid.NewGuid();
        var ground = new BallState(Vector3.Zero, new(float.MaxValue, 0, float.MaxValue), 0.5f, true, Vector3.UnitY, 0, 0);
        presenter.Apply(new(id, level.Id, 0, 0, SessionPhase.Running, ground, [], []), GameSkins.All[0]);
        presenter.Apply(new(id, level.Id, 1, 1e100, SessionPhase.Running,
            ground with { Grounded = false, GroundNormal = Vector3.Zero }, [], []), GameSkins.All[0]);
        Assert.AreEqual(1f, presenter.BallObject.Transform.Rotation.Length(), 1e-6f);
    }

    [TestMethod]
    public void MutingShowsZeroButUnmuteRestoresPreferenceAndSliderChangesUnmute()
    {
        var settings = new UserSettings { MasterVolume = 0.6f };
        var catalog = GameLevels.CreateCatalog();
        var flow = new LevelFlow(catalog, SimulationSettings.Default, settings);
        var volumes = new List<float>();
        var menu = new MenuController(flow, catalog, volumes.Add);
        flow.OpenSettings();
        MenuView View() => menu.BuildView(800, 450);
        void Click(string id, float fraction = 0.5f)
        {
            var bounds = View().Controls.Single(control => control.Id == id).Bounds;
            menu.Handle(new(default, false, false, new(0, 0, false, false, true,
                new(bounds.X + bounds.Width * fraction, bounds.Y + bounds.Height / 2))), 800, 450);
        }
        Click("mute");
        Assert.IsTrue(settings.Muted);
        Assert.AreEqual(0.6f, settings.MasterVolume);
        Assert.AreEqual(0f, volumes[^1]);
        Assert.AreEqual("Master volume: 0%", View().Controls.Single(control => control.Id == "volume").Label);
        var track = View().Controls.Single(control => control.Id == "volume").ValueBounds!.Value;
        Assert.IsFalse(View().Panels.Any(panel => panel.Bounds.Y == track.Y && panel.Bounds.Width > 0 && panel.Bounds.Width < track.Width));
        Click("mute");
        Assert.IsFalse(settings.Muted);
        Assert.AreEqual(0.6f, volumes[^1]);
        Click("mute");
        Click("volume", 0.25f);
        Assert.IsFalse(settings.Muted);
        Assert.AreEqual(0.25f, settings.MasterVolume);
        Assert.AreEqual(0.25f, volumes[^1]);
        Click("mute");
        Click("volume", 0f);
        Assert.IsTrue(settings.Muted);
        Assert.AreEqual(0.25f, settings.MasterVolume);
        menu.Handle(new(default, false, false, new(1, 0, false, false, false, default)), 800, 450);
        Assert.IsFalse(settings.Muted);
        Assert.AreEqual(0.05f, settings.MasterVolume);
        Assert.AreEqual(0.05f, volumes[^1]);
    }

    [TestMethod]
    public void NavigationLegendDoesNotClutterMenuScreens()
    {
        var catalog = GameLevels.CreateCatalog();
        var flow = new LevelFlow(catalog, SimulationSettings.Default, new());
        var menu = new MenuController(flow, catalog);
        foreach (bool settings in new[] { false, true })
        {
            if (settings) flow.OpenSettings();
            var texts = menu.BuildView(800, 450).Texts.Select(text => text.Text).ToArray();
            CollectionAssert.DoesNotContain(texts, "Enter / Space / Mouse click: select");
            CollectionAssert.DoesNotContain(texts, "Tab / Shift+Tab: next / previous");
            Assert.IsFalse(texts.Any(text => text.StartsWith("Up / Down: choose", StringComparison.Ordinal)));
        }
    }

    [TestMethod]
    public void AirborneBallKeepsTakeoffSpinWithoutCameraInputOrGroundNormal()
    {
        var level = TrainingTrack.Create();
        var presenter = new GameScenePresenter(level);
        var id = Guid.NewGuid();
        SessionSnapshot Snapshot(long tick, double seconds, Vector3 position, Vector3 velocity, bool grounded) =>
            new(id, level.Id, tick, seconds, SessionPhase.Running,
                new(position, velocity, 0.5f, grounded, grounded ? Vector3.UnitY : Vector3.Zero, 0, 0), [], []);
        presenter.Apply(Snapshot(0, 0, Vector3.Zero, Vector3.UnitX, true), GameSkins.All[0]);
        var airborne = Snapshot(1, 0.25, new(0.25f, 1, 0), new(1, 3, 0), false);
        presenter.Apply(airborne, GameSkins.All[0]);
        var expected = Quaternion.CreateFromAxisAngle(-Vector3.UnitZ, 0.5f); // speed/radius * time.
        Assert.IsGreaterThan(0.99999f, MathF.Abs(Quaternion.Dot(expected, presenter.BallObject.Transform.Rotation)));
        var rotation = presenter.BallObject.Transform.Rotation;
        presenter.Apply(airborne, GameSkins.All[1]);
        Assert.AreEqual(rotation, presenter.BallObject.Transform.Rotation);
        presenter.Apply(Snapshot(2, 0.5, new(0.5f, 0.5f, 0), new(0, -5, 0), false), GameSkins.All[1]);
        Assert.IsLessThan(0.999f, MathF.Abs(Quaternion.Dot(rotation, presenter.BallObject.Transform.Rotation)));
    }
}
