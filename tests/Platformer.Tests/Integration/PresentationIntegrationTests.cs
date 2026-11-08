using Microsoft.VisualStudio.TestTools.UnitTesting;
using Platformer.App;
using Platformer.Core;

namespace Platformer.Tests.Integration;

/// <summary>Стык LevelFlow snapshot → HUD/MenuView → GameScenePresenter → публичный Scene (#10).</summary>
[TestClass]
public sealed class PresentationIntegrationTests
{
    [TestMethod]
    public void CoinCountHudAndCoinVisibilityChangeInTheSameFrame()
    {
        var driver = new IntegrationDriver();
        driver.Press(AppButtons.Enter);
        var coin = driver.Flow.Presenter!.CoinObjects["spawn-coin"];
        Assert.AreEqual("Coins 0/2", driver.HudText("Coins"));
        Assert.Contains(coin, driver.Host.Scene.Objects);

        Assert.AreEqual(1, driver.Frame(seconds: driver.FixedStep).Ticks);
        // Один Advance: snapshot, HUD и render scene согласованы, без отставания на кадр.
        Assert.AreEqual(1, driver.Snapshot.CoinCount);
        Assert.AreEqual("Coins 1/2", driver.HudText("Coins"));
        Assert.DoesNotContain(coin, driver.Host.Scene.Objects);
        Assert.Contains(driver.Flow.Presenter.CoinObjects["far-coin"], driver.Host.Scene.Objects);

        driver.Press(AppButtons.R);
        Assert.AreEqual(0, driver.Snapshot.CoinCount);
        Assert.AreEqual("Coins 0/2", driver.HudText("Coins"));
        Assert.Contains(coin, driver.Host.Scene.Objects);
    }

    [TestMethod]
    public void HudFollowsCurrentLevelAfterNextAndDisappearsOutsidePlaying()
    {
        var driver = new IntegrationDriver();
        driver.Press(AppButtons.Enter);
        driver.HoldUntilLeavesPlaying(AppButtons.D);
        var texts = driver.Host.View.Texts.Select(text => text.Text).ToArray();
        Assert.IsFalse(texts.Any(text => text.StartsWith("Boost", StringComparison.Ordinal)),
            "Игровой HUD не рисуется поверх экрана результата");
        Assert.AreEqual(1, texts.Count(text => text == "Coins 1/2"), "Итог уровня берётся из того же snapshot");
        driver.Press(AppButtons.Enter);
        Assert.AreEqual("Coins 0/0", driver.HudText("Coins"));
        Assert.IsTrue(driver.Host.View.Texts.Any(text => text.Text == HudModel.Create(
            IntegrationDriver.TwoLevels().Get(IntegrationDriver.Second), driver.Snapshot).LevelName));
    }

    [TestMethod]
    public void SelectedSkinAndVolumeSurviveRestartNextAndBothLocationsShareOneScene()
    {
        var driver = new IntegrationDriver();
        var scene = driver.Host.Scene;
        var orange = GameSkins.Get("orange").Material;
        driver.Click("skins");
        driver.Click("skin:orange");
        driver.Press(AppButtons.Escape);
        driver.Click("settings");
        driver.Click("mute");
        driver.Press(AppButtons.Escape);
        Assert.AreEqual(0f, driver.AppliedVolumes[^1]);
        int volumeCalls = driver.AppliedVolumes.Count;

        driver.Press(AppButtons.Enter);
        var firstPresenter = driver.Flow.Presenter!;
        Assert.AreSame(orange, firstPresenter.BallObject.Material);
        driver.Press(AppButtons.R);
        Assert.AreSame(firstPresenter, driver.Flow.Presenter, "Restart переиспользует presenter локации");
        Assert.AreSame(orange, driver.Flow.Presenter!.BallObject.Material);

        driver.HoldUntilLeavesPlaying(AppButtons.D);
        driver.Press(AppButtons.Enter);
        var secondPresenter = driver.Flow.Presenter!;
        Assert.AreNotSame(firstPresenter, secondPresenter);
        Assert.AreSame(orange, secondPresenter.BallObject.Material);
        Assert.AreSame(scene, driver.Host.Scene, "Engine.Run удерживает один Scene для обеих локаций");
        CollectionAssert.AreEquivalent(secondPresenter.Scene.Objects.ToArray(), scene.Objects.ToArray());
        Assert.IsFalse(firstPresenter.Scene.Objects.Any(scene.Objects.Contains), "Объекты level1 убраны из сцены");
        Assert.IsTrue(driver.Flow.UserSettings.Muted);
        Assert.AreEqual(volumeCalls, driver.AppliedVolumes.Count, "Переходы уровней не переприменяют громкость");
        driver.Press(AppButtons.Escape);
        driver.Click("settings");
        Assert.AreEqual("Mute: ON", driver.Host.View.Controls.Single(control => control.Id == "mute").Label);
    }
}
