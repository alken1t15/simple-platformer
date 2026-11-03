using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Platformer.App;

namespace Platformer.Tests;

[TestClass]
public sealed class InputRouterTests
{
    [TestMethod]
    public void MovementUsesHorizontalYawAndNormalizesDiagonals()
    {
        var router = new InputRouter();
        Vector2 Move(AppButtons held, float yaw = 0f) => router.Map(new(true, held), AppScreen.Playing, yaw).Gameplay.MoveWorldXZ;
        Assert.AreEqual(-Vector2.UnitY, Move(AppButtons.W));
        Assert.AreEqual(Vector2.UnitX, Move(AppButtons.D));
        Assert.AreEqual(Vector2.Zero, Move(AppButtons.W | AppButtons.S | AppButtons.A | AppButtons.D));
        Assert.AreEqual(1f, Move(AppButtons.W | AppButtons.D).Length(), 1e-6f);
        Assert.IsLessThan(1e-6f, Vector2.Distance(-Vector2.UnitX, Move(AppButtons.W, MathF.PI / 2)));
        Assert.IsLessThan(1e-6f, Vector2.Distance(-Vector2.UnitY, Move(AppButtons.D, MathF.PI / 2)));
    }

    [TestMethod]
    public void GameplayEdgesAndMenuAreRoutedOnlyToTheirScreen()
    {
        var router = new InputRouter();
        AppButtons edges = AppButtons.Space | AppButtons.LeftShift | AppButtons.RightShift | AppButtons.R | AppButtons.Escape;
        var input = new AppInputSnapshot(true, edges, edges);
        var playing = router.Map(input, AppScreen.Playing, 0f);
        Assert.IsTrue(playing.Gameplay.JumpPressed && playing.Gameplay.BoostPressed && playing.RestartPressed && playing.PausePressed);
        Assert.AreEqual(default, playing.Menu);
        foreach (AppScreen screen in Enum.GetValues<AppScreen>().Where(s => s != AppScreen.Playing))
        {
            var menu = router.Map(input, screen, 0f);
            Assert.AreEqual(default, menu.Gameplay);
            Assert.IsFalse(menu.PausePressed);
            Assert.IsTrue(menu.Menu.BackPressed && menu.Menu.ActivatePressed);
            Assert.AreEqual(screen is AppScreen.Paused or AppScreen.Failed, menu.RestartPressed);
        }
        var held = router.Map(input with { Pressed = AppButtons.None }, AppScreen.Playing, 0f);
        Assert.IsFalse(held.Gameplay.JumpPressed || held.Gameplay.BoostPressed || held.RestartPressed || held.PausePressed);
    }

    [TestMethod]
    public void MenuOppositesCancelAndPointerUsesSameSnapshot()
    {
        var router = new InputRouter();
        var input = new AppInputSnapshot(true, Pressed: AppButtons.Up | AppButtons.Down | AppButtons.Tab
            | AppButtons.Left | AppButtons.Right | AppButtons.PointerLeft, PointerPosition: new(123f, 45f));
        var menu = router.Map(input, AppScreen.MainMenu, 0f).Menu;
        Assert.AreEqual(0, menu.HorizontalStep);
        Assert.AreEqual(0, menu.VerticalStep);
        Assert.IsTrue(menu.PointerPressed);
        Assert.AreEqual(input.PointerPosition, menu.PointerPosition);
        Assert.AreEqual(1, router.Map(new(true, Pressed: AppButtons.Tab), AppScreen.MainMenu, 0f).Menu.VerticalStep);
    }

    [TestMethod]
    public void FocusGainAndTransitionsRequireReleaseBeforeNewPress()
    {
        var router = new InputRouter();
        AppButtons keys = AppButtons.W | AppButtons.Space | AppButtons.LeftShift | AppButtons.PointerLeft;
        var held = new AppInputSnapshot(true, keys, keys);
        Assert.AreEqual(default, router.Map(held with { IsFocused = false }, AppScreen.Playing, 0f));
        Assert.AreEqual(default, router.Map(held, AppScreen.Playing, 0f));
        Assert.AreEqual(default, router.Map(held with { Pressed = AppButtons.None }, AppScreen.Playing, 0f));
        router.Map(new(true), AppScreen.Playing, 0f);
        var fresh = router.Map(held, AppScreen.Playing, 0f);
        Assert.IsTrue(fresh.Gameplay.JumpPressed && fresh.Gameplay.BoostPressed);
        Assert.AreEqual(-Vector2.UnitY, fresh.Gameplay.MoveWorldXZ);
        router.Clear(held);
        Assert.AreEqual(default, router.Map(held, AppScreen.Playing, 0f));
    }
}
