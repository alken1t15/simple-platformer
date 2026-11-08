using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Platformer.App;
using Platformer.Core;

namespace Platformer.Tests;

[TestClass]
public sealed class GameFlowFeedbackTests
{
    [TestMethod]
    public void HeldJumpReachesEveryCatchUpTickWhileBoostRemainsAnEdge()
    {
        var runner = new FixedStepRunner(SimulationSettings.Default);
        var commands = new List<GameCommand>();
        runner.Advance(runner.FixedStepSeconds * 3, new(Vector2.Zero, true, true), commands.Add, jumpHeld: true);
        Assert.HasCount(3, commands);
        Assert.IsTrue(commands.All(command => command.JumpPressed));
        Assert.AreEqual(1, commands.Count(command => command.BoostPressed));
        commands.Clear();
        runner.Advance(runner.FixedStepSeconds, default, commands.Add, jumpHeld: false);
        Assert.IsFalse(commands.Single().JumpPressed);
    }

    [TestMethod]
    public void AutoJumpCannotLeakFromMenuActivationOrThroughPauseAndFocusRearm()
    {
        var catalog = new LevelCatalog([SessionFixtures.Level(finishX: 100)]);
        var host = new GameplayHost(catalog, SimulationSettings.Default, new());
        host.Advance(new(true), 0);
        host.Advance(new(true, AppButtons.Space, AppButtons.Space), 0); // Play через Space.
        Assert.AreEqual(AppScreen.Playing, host.Flow.Screen);
        for (int i = 0; i < 50; i++) host.Advance(new(true, AppButtons.Space), 0.02);
        Assert.IsFalse(host.Flow.DrainEvents().Any(e => e.Kind == GameEventKind.Jumped));
        host.Advance(new(true), 0.02);
        host.Advance(new(true, AppButtons.Space, AppButtons.Space), 0.02);
        Assert.IsTrue(host.Flow.DrainEvents().Any(e => e.Kind == GameEventKind.Jumped));
        host.Advance(new(false, AppButtons.Space), 0.02);
        host.Advance(new(true, AppButtons.Space), 0.02);
        Assert.AreEqual(AppScreen.Paused, host.Flow.Screen);
        host.Flow.Continue();
        host.Advance(new(true, AppButtons.Space), 0);
        for (int i = 0; i < 70; i++) host.Advance(new(true, AppButtons.Space), 0.02);
        Assert.IsFalse(host.Flow.DrainEvents().Any(e => e.Kind == GameEventKind.Jumped));
        Assert.IsTrue(host.Flow.Snapshot!.Ball.Grounded);
    }

    [TestMethod]
    public void ConfirmationIgnoresRestartAndFocusLossAndRequestsInputRearm()
    {
        var catalog = new LevelCatalog([SessionFixtures.Level(finishX: 100)]);
        var flow = new LevelFlow(catalog, SimulationSettings.Default, new());
        var menu = new MenuController(flow, catalog);
        flow.Start(catalog.Levels[0].Id);
        flow.Pause();
        var snapshot = flow.Snapshot;
        Assert.IsTrue(Click(menu, "main-menu").ResetInput);
        menu.Handle(new(default, true, false, default), 960, 640);
        Assert.AreSame(snapshot, flow.Snapshot);
        menu.Handle(default, 960, 640, isFocused: false);
        Assert.AreSame(snapshot, flow.Snapshot);
        Assert.IsTrue(menu.BuildView(960, 640).Texts.Any(text => text.Text == "Leave this run?"));
        Assert.IsTrue(Click(menu, "cancel-main-menu").ResetInput);
        Assert.AreSame(snapshot, flow.Snapshot);
    }

    private static MenuUpdateResult Click(MenuController menu, string id)
    {
        var bounds = menu.BuildView(960, 640).Controls.Single(control => control.Id == id).Bounds;
        return menu.Handle(new(default, false, false, new(0, 0, false, false, true,
            new(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2))), 960, 640);
    }

    [TestMethod]
    public void ActivatingLevelAndSkinCardsAppliesWithoutExtraButtons()
    {
        var catalog = new LevelCatalog([SessionFixtures.Level(), SessionFixtures.Level("level2")]);
        var settings = new UserSettings();
        var flow = new LevelFlow(catalog, SimulationSettings.Default, settings);
        var menu = new MenuController(flow, catalog);
        flow.OpenSkins();
        Click(menu, "skin:orange");
        Assert.AreEqual("orange", settings.SkinId);
        Assert.IsFalse(menu.BuildView(960, 640).Controls.Any(control => control.Id == "select"));
        Click(menu, "back");
        flow.OpenLevelSelect();
        Assert.IsFalse(menu.BuildView(960, 640).Controls.Any(control => control.Id == "start"));
        Assert.IsTrue(Click(menu, "level:level2").ResetCamera);
        Assert.AreEqual(AppScreen.Playing, flow.Screen);
        Assert.AreEqual("level2", flow.Snapshot!.LevelId);
    }

    [TestMethod]
    public void LeavingRunRequiresConfirmationAndCancelKeepsSessionAndPause()
    {
        var catalog = new LevelCatalog([SessionFixtures.Level(finishX: 100)]);
        var flow = new LevelFlow(catalog, SimulationSettings.Default, new());
        var menu = new MenuController(flow, catalog);
        flow.Start(catalog.Levels[0].Id);
        flow.Step(default);
        flow.Pause();
        var snapshot = flow.Snapshot;
        Click(menu, "main-menu");
        Assert.AreEqual(AppScreen.Paused, flow.Screen);
        Assert.AreSame(snapshot, flow.Snapshot);
        Assert.IsTrue(menu.BuildView(960, 640).Controls.Single(control => control.Id == "cancel-main-menu").Selected);
        menu.Handle(new(default, false, false, new(0, 0, true, false, false, default)), 960, 640);
        Assert.AreEqual(AppScreen.Paused, flow.Screen);
        Assert.IsTrue(menu.BuildView(960, 640).Controls.Any(control => control.Id == "main-menu"));
        Click(menu, "main-menu");
        menu.Handle(new(default, false, false, new(0, 0, false, true, false, default)), 960, 640);
        Assert.AreSame(snapshot, flow.Snapshot);
        Assert.AreEqual(AppScreen.Paused, flow.Screen);
        Click(menu, "main-menu");
        Click(menu, "confirm-main-menu");
        Assert.AreEqual(AppScreen.MainMenu, flow.Screen);
        Assert.IsNull(flow.Snapshot);
    }

    [TestMethod]
    public void MainMenuKeepsCollectedCoinsHiddenInFrozenBackdrop()
    {
        var level = SessionFixtures.Level(coins: [new("coin", new(0, 0.501f, 0), 0.15f)], finishX: 100);
        var host = new GameplayHost(new LevelCatalog([level]), SimulationSettings.Default, new());
        host.Flow.Start(level.Id);
        host.Flow.Step(default);
        host.Advance(new(true), 0);
        Assert.AreEqual(1, host.Flow.Snapshot!.CoinCount);
        Assert.IsFalse(host.Scene.Objects.Any(item => ReferenceEquals(item.Mesh, GameMeshes.Coin)));
        var objects = host.Scene.Objects.ToArray();
        host.Flow.MainMenu();
        host.Advance(new(true), 0);
        Assert.IsFalse(host.Scene.Objects.Any(item => ReferenceEquals(item.Mesh, GameMeshes.Coin)));
        CollectionAssert.AreEqual(objects, host.Scene.Objects.ToArray());
    }

    [TestMethod]
    public void HeldSpaceJumpsAgainAfterLandingAndReleaseStopsAutoJump()
    {
        var level = SessionFixtures.Level(finishX: 100);
        var host = new GameplayHost(new LevelCatalog([level]), SimulationSettings.Default, new());
        host.Flow.Start(level.Id);
        host.Advance(new(true), 0.03);
        int jumps = 0;
        for (int i = 0; i < 150; i++)
        {
            host.Advance(new(true, AppButtons.Space, i == 0 ? AppButtons.Space : 0), 0.02);
            jumps += host.Flow.DrainEvents().Count(e => e.Kind == GameEventKind.Jumped);
        }
        Assert.IsGreaterThanOrEqualTo(2, jumps);
        for (int i = 0; i < 80; i++)
        {
            host.Advance(new(true), 0.02);
            Assert.IsFalse(host.Flow.DrainEvents().Any(e => e.Kind == GameEventKind.Jumped));
        }
        Assert.IsTrue(host.Flow.Snapshot!.Ball.Grounded);
    }
}
