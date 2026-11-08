using System.Globalization;
using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Platformer.App;
using Platformer.Core;

namespace Platformer.Tests;

[TestClass]
public sealed class HudModelTests
{
    [TestMethod]
    public void HudUsesLevelTotalsSnapshotTimersAndInvariantEnglishNumbers()
    {
        var level = TrainingTrack.Create();
        var ball = new BallSimulator(level, SimulationSettings.Default).CreateInitialState();
        SessionSnapshot Snapshot(float active, float cooldown, double seconds = 0d) => new(Guid.NewGuid(), level.Id, 0,
            seconds, SessionPhase.Running, ball with { BoostRemainingSeconds = active, BoostCooldownSeconds = cooldown },
            [level.Coins[0].Id, level.Coins[1].Id], []);
        var culture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ru-RU");
            var hud = HudModel.Create(level, Snapshot(1.26f, 4f));
            Assert.AreEqual("Training Track", hud.LevelName);
            Assert.AreEqual(2, hud.CoinCount);
            Assert.AreEqual(level.Coins.Count, hud.TotalCoins);
            Assert.AreEqual(BoostStatus.Active, hud.Boost);
            Assert.AreEqual("Boost active: 1.3s", hud.BoostText);
            hud = HudModel.Create(level, Snapshot(0f, 2.5f));
            Assert.AreEqual(BoostStatus.Cooldown, hud.Boost);
            Assert.AreEqual("Boost cooldown: 2.5s", hud.BoostText);
            hud = HudModel.Create(level, Snapshot(0f, 0f, 8d));
            Assert.AreEqual("Boost ready", hud.BoostText);
            Assert.IsFalse(hud.ShowHelp);
            Assert.IsTrue(HudModel.Create(level, Snapshot(0f, 0f)).ShowHelp);
        }
        finally { CultureInfo.CurrentCulture = culture; }
        Assert.ThrowsExactly<ArgumentException>(() => HudModel.Create(SessionFixtures.Level("other"), Snapshot(0f, 0f)));
    }

    [TestMethod]
    public void PlayingHasOneHudAndNoClickableMenuOrSimulationSideEffects()
    {
        var level = TrainingTrack.Create();
        var catalog = new LevelCatalog([level]);
        var flow = new LevelFlow(catalog, SimulationSettings.Default, new());
        var menu = new MenuController(flow, catalog);
        flow.Start(level.Id);
        var before = flow.Snapshot;
        var view = menu.BuildView(1280f, 720f);
        Assert.AreEqual(AppScreen.Playing, view.Screen);
        Assert.IsEmpty(view.Controls);
        Assert.HasCount(1, view.Texts.Where(text => text.Text == "Coins 0/10").ToArray());
        Assert.HasCount(3, view.Texts);
        Assert.IsFalse(view.Texts.Any(text => text.Text.Contains("WASD")));
        menu.Handle(new(default, false, false, new(0, 1, true, false, true, Vector2.One)), 1280f, 720f);
        Assert.AreSame(before, flow.Snapshot);
        Assert.AreEqual(AppScreen.Playing, flow.Screen);
    }
}
