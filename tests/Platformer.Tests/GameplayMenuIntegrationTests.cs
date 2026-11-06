using System.Numerics;
using Engine3D.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Platformer.App;
using Platformer.Core;

namespace Platformer.Tests;

[TestClass]
public sealed class GameplayMenuIntegrationTests
{
    [TestMethod]
    public void HoldingForwardAtTrainingGapDoesNotFaultGameplayHost()
    {
        var track = TrainingTrack.Create();
        float top = 3f * MathF.Tan(MathF.PI / 9f);
        var level = new LevelDefinition(track.Id, new(7f, top + 0.501f, 10f), track.KillY,
            track.Finish, track.Surfaces, track.Coins, track.Hazards);
        var host = new GameplayHost(new LevelCatalog([level]), SimulationSettings.Default, new UserSettings());
        Click(host, "play");
        for (int frame = 0; frame < 150; frame++)
            host.Advance(new(true, AppButtons.S), 4 * host.Frames.Clock.FixedStepSeconds);
        Assert.IsFalse(host.Frames.Clock.IsFaulted);
        Assert.IsGreaterThan(141L, host.Flow.Snapshot!.Tick);
        host.Camera.Validate();
    }

    private static GameplayHost Create(Action<float>? volume = null) => new(
        new LevelCatalog([SessionFixtures.Level(finishX: 50f)]), SimulationSettings.Default,
        new UserSettings(), applyMasterVolume: volume);

    private static void Click(GameplayHost host, string id, bool enter = false, float width = 960f, float height = 640f)
    {
        host.Advance(new(true), 0d, width, height);
        var bounds = host.View.Controls.Single(control => control.Id == id).Bounds;
        var buttons = AppButtons.PointerLeft | (enter ? AppButtons.Enter : AppButtons.None);
        host.Advance(new(true, buttons, buttons,
            PointerPosition: new(bounds.X + bounds.Width / 2f, bounds.Y + bounds.Height / 2f)), 0d, width, height);
    }

    private static void Key(GameplayHost host, AppButtons key)
    {
        host.Advance(new(true), 0d);
        host.Advance(new(true, key, key), 0d);
    }

    [TestMethod]
    public void ClickAndEnterStartExactlyOneSessionAndHudUsesItsSnapshot()
    {
        var host = Create();
        var scene = host.Scene;
        Click(host, "play", enter: true);
        Guid id = host.Flow.Snapshot!.SessionId;
        Assert.AreEqual(0L, host.Flow.Snapshot.Tick);
        Assert.IsTrue(host.CaptureRequested);
        host.Advance(new(true, AppButtons.Enter | AppButtons.PointerLeft), host.Frames.Clock.FixedStepSeconds);
        Assert.AreEqual(id, host.Flow.Snapshot.SessionId);
        Assert.AreEqual(1L, host.Flow.Snapshot.Tick);
        Assert.AreSame(scene, host.Scene);
        var overlay = MenuOverlayAdapter.Build(host.View);
        Assert.IsTrue(overlay.Texts.ToArray().Any(text => text.Text == "Training Track"));
        Assert.IsTrue(overlay.Texts.ToArray().Any(text => text.Text == "Coins 0/0"));
        Assert.IsTrue(overlay.Texts.ToArray().Any(text => text.Text == "Boost ready"));
        Assert.HasCount(0, host.View.Controls);
    }

    [TestMethod]
    public void SettingsAndSkinApplyThroughHostBeforeFirstStart()
    {
        var volumes = new List<float>();
        var host = Create(volumes.Add);
        CollectionAssert.AreEqual(new[] { 1f }, volumes);
        Click(host, "settings");
        Key(host, AppButtons.Left);
        Assert.AreEqual(0.95f, volumes[^1], 1e-6f);
        Click(host, "mute");
        Assert.AreEqual(0f, volumes[^1]);
        Click(host, "back");
        Click(host, "skins");
        Click(host, "skin:orange");
        Click(host, "select");
        Click(host, "back");
        Click(host, "play");
        Assert.AreEqual("orange", host.Flow.UserSettings.SkinId);
        Assert.AreEqual(GameSkins.Get("orange").Material.BaseColor, host.Flow.Presenter!.BallObject.Material.BaseColor);
        Assert.AreEqual(0L, host.Flow.Snapshot!.Tick);
    }

    [TestMethod]
    public void OverlayAndHitTestUseSameLogicalBoundsAfterResize()
    {
        var host = Create();
        host.RefreshView(640f, 480f);
        var button = host.View.Controls.Single(control => control.Id == "settings");
        var overlay = MenuOverlayAdapter.Build(host.View);
        var rect = new UiRect(button.Bounds.X, button.Bounds.Y, button.Bounds.Width, button.Bounds.Height);
        Assert.IsTrue(overlay.Panels.ToArray().Any(panel => panel.Bounds == rect));
        Click(host, "settings", width: 640f, height: 480f);
        Assert.AreEqual(AppScreen.Settings, host.Flow.Screen);
        Assert.AreEqual(new Vector2(640f, 480f), host.View.ClientSize);
        host.RefreshView(0f, 0f);
        var minimized = MenuOverlayAdapter.Build(host.View);
        Assert.AreEqual(0, minimized.Panels.Length);
        Assert.AreEqual(0, minimized.Texts.Length);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => host.Advance(new(true), 0d, float.NaN, 640f));
        Assert.AreEqual(AppScreen.Settings, host.Flow.Screen);
    }

    [TestMethod]
    public void PauseSettingsAndEscapeBackPreserveSessionAndDiscardTime()
    {
        var host = Create();
        Click(host, "play");
        host.Advance(new(true), host.Frames.Clock.FixedStepSeconds);
        long tick = host.Flow.Snapshot!.Tick;
        Key(host, AppButtons.Escape);
        Assert.AreEqual(AppScreen.Paused, host.Flow.Screen);
        Click(host, "settings");
        host.Advance(new(true), 20d);
        Assert.AreEqual(tick, host.Flow.Snapshot.Tick);
        Assert.IsFalse(host.CaptureRequested);
        Key(host, AppButtons.Escape);
        Assert.AreEqual(AppScreen.Paused, host.Flow.Screen);
        Key(host, AppButtons.Escape);
        Assert.AreEqual(AppScreen.Playing, host.Flow.Screen);
        Assert.AreEqual(tick, host.Flow.Snapshot.Tick);
        Assert.AreEqual(0d, host.Frames.Clock.AccumulatedSeconds);
        Assert.IsTrue(host.CaptureRequested);
    }

    [TestMethod]
    public void ReleasedCapturePointerDoesNotChangeDefaultContinueSelection()
    {
        var host = Create();
        Click(host, "play");
        host.Advance(new(true, PointerPosition: new(10000f, 10000f)), 0d);
        host.Advance(new(true, AppButtons.Escape, AppButtons.Escape, PointerPosition: new(10000f, 10000f)), 0d);
        host.Advance(new(true, PointerPosition: new(480f, 340f)), 0d);
        Assert.IsTrue(host.View.Controls.Single(control => control.Id == "continue").Selected,
            "Освобождение виртуального Captured указателя не является движением мыши по меню.");
    }

    [TestMethod]
    public void TerminalTickPublishesResultOverlayAndStopsCatchUp()
    {
        var host = new GameplayHost(new LevelCatalog([SessionFixtures.Level(finishX: 0f)]),
            SimulationSettings.Default, new UserSettings());
        Click(host, "play");
        Assert.AreEqual(1, host.Advance(new(true), 0.1d).Ticks);
        Assert.AreEqual(AppScreen.LevelResult, host.View.Screen);
        var overlay = MenuOverlayAdapter.Build(host.View);
        Assert.IsTrue(overlay.Texts.ToArray().Any(text => text.Text == "Level complete!"));
        Assert.IsFalse(host.CaptureRequested);
        Click(host, "next");
        Assert.AreEqual(AppScreen.GameComplete, host.View.Screen);
        Assert.IsTrue(MenuOverlayAdapter.Build(host.View).Texts.ToArray().Any(text => text.Text == "Adventure complete!"));
        Assert.AreEqual(0d, host.Frames.Clock.AccumulatedSeconds);
    }
}
