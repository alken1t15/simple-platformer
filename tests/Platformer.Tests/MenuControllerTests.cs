using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Platformer.App;
using Platformer.Core;

namespace Platformer.Tests;

[TestClass]
public sealed class MenuControllerTests
{
    private const float Width = 1280f, Height = 720f;

    private sealed class Fixture
    {
        public LevelCatalog Catalog { get; } = new([
            SessionFixtures.Level("level1", [new("coin1", new(1f, 0.501f, 0f), 0.15f)], floorHalfWidth: 2f),
            SessionFixtures.Level("level2", [new("coin2", new(1f, 0.501f, 0f), 0.15f)])]);
        public List<float> Volumes { get; } = [];
        public List<bool> AudioPause { get; } = [];
        public int Resets { get; private set; }
        public LevelFlow Flow { get; }
        public MenuController Menu { get; }

        public Fixture(UserSettings? settings = null)
        {
            Flow = new(Catalog, SimulationSettings.Default, settings ?? new(), () => Resets++,
                setAudioPaused: AudioPause.Add);
            Menu = new(Flow, Catalog, Volumes.Add);
        }

        public MenuView View(float width = Width, float height = Height) => Menu.BuildView(width, height);
        public MenuUpdateResult Send(MenuInput input = default, bool restart = false, bool pause = false, bool focused = true) =>
            Menu.Handle(new(default, restart, pause, input), Width, Height, focused);
        public MenuUpdateResult Click(string id, bool enter = false, float fraction = 0.5f)
        {
            var bounds = View().Controls.Single(control => control.Id == id).Bounds;
            return Send(new(0, 0, enter, false, true, new(bounds.X + bounds.Width * fraction, bounds.Y + bounds.Height / 2f)));
        }
        public void Finish() => SessionFixtures.Until(() => Flow.Screen == AppScreen.LevelResult,
            () => Flow.Step(SessionFixtures.Right));
    }

    [TestMethod]
    public void MainMenuOpensEachSubmenuAndOnlyExitCloses()
    {
        var f = new Fixture();
        CollectionAssert.AreEqual(new[] { "Play", "Levels", "Settings", "Skins", "Exit" },
            f.View().Controls.Select(control => control.Label).ToArray());
        foreach (var (id, screen) in new[] { ("levels", AppScreen.LevelSelect), ("settings", AppScreen.Settings), ("skins", AppScreen.SkinSelect) })
        {
            f.Click(id);
            Assert.AreEqual(screen, f.Flow.Screen);
            f.Click("back");
            Assert.AreEqual(AppScreen.MainMenu, f.Flow.Screen);
        }
        f.Send(new(0, 0, false, true, false, default));
        Assert.IsFalse(f.Menu.CloseRequested);
        f.Click("exit");
        Assert.IsTrue(f.Menu.CloseRequested);
        f.Click("play");
        Assert.AreEqual(AppScreen.MainMenu, f.Flow.Screen);
        Assert.IsNull(f.Flow.Snapshot);
    }

    [TestMethod]
    public void MouseTargetWinsOverKeyboardActivationAndExecutesOnlyOnce()
    {
        var f = new Fixture(new() { MasterVolume = 0.35f });
        f.Click("settings", enter: true); // Enter по умолчанию выбирал бы Play.
        Assert.AreEqual(AppScreen.Settings, f.Flow.Screen);
        f.Click("mute", enter: true);
        Assert.IsTrue(f.Flow.UserSettings.Muted);
        CollectionAssert.AreEqual(new[] { 0.35f, 0f }, f.Volumes);
        f.Click("back");
        int resets = f.Resets;
        Assert.IsTrue(f.Click("play", enter: true).ResetCamera);
        Assert.AreEqual(resets + 1, f.Resets);
        Assert.AreEqual(0, f.Flow.Snapshot!.Tick);
    }

    [TestMethod]
    public void PointerHoverChangesSelectionWithoutActivationAndStationaryPointerAllowsKeyboard()
    {
        var f = new Fixture();
        var bounds = f.View().Controls.Single(control => control.Id == "settings").Bounds;
        Vector2 pointer = new(bounds.X + 20f, bounds.Y + 20f);
        f.Send(new(0, 0, false, false, false, pointer));
        Assert.IsTrue(f.View().Controls.Single(control => control.Id == "settings").Selected);
        Assert.IsTrue(f.View().Controls.Single(control => control.Id == "settings").Hovered);
        Assert.AreEqual(AppScreen.MainMenu, f.Flow.Screen);
        f.Send(new(0, 1, false, false, false, pointer));
        Assert.IsTrue(f.View().Controls.Single(control => control.Id == "skins").Selected);
        f.Send(new(0, 0, true, false, false, pointer));
        Assert.AreEqual(AppScreen.SkinSelect, f.Flow.Screen);
    }

    [TestMethod]
    public void StationaryPointerDoesNotReplaceKeyboardSelectionAfterOpeningAnotherScreen()
    {
        var f = new Fixture();
        Vector2 pointer = new(500f, 326f); // Levels в MainMenu, Mute в Settings.
        f.Send(new(0, 0, false, false, false, pointer));
        f.Send(new(0, 1, false, false, false, pointer));
        f.Send(new(0, 0, true, false, false, pointer));
        Assert.AreEqual(AppScreen.Settings, f.Flow.Screen);
        f.Send(new(0, 0, true, false, false, pointer));
        Assert.IsFalse(f.Flow.UserSettings.Muted);
        Assert.IsTrue(f.View().Controls.Single(control => control.Id == "volume").Selected);
    }

    [TestMethod]
    public void UpDownTabEnterSpaceAndEscapeUseTheExistingInputRouter()
    {
        var f = new Fixture();
        var router = new InputRouter();
        void Press(AppButtons key)
        {
            var snapshot = new AppInputSnapshot(true, key, key);
            f.Menu.Handle(router.Map(snapshot, f.Flow.Screen, 0f), Width, Height);
            router.Map(new AppInputSnapshot(true), f.Flow.Screen, 0f);
        }
        Press(AppButtons.Up);
        Assert.IsTrue(f.View().Controls.Single(control => control.Id == "exit").Selected);
        Press(AppButtons.Down);
        Press(AppButtons.Tab);
        Assert.IsTrue(f.View().Controls.Single(control => control.Id == "levels").Selected);
        Press(AppButtons.Space);
        Assert.AreEqual(AppScreen.LevelSelect, f.Flow.Screen);
        Press(AppButtons.Escape);
        Press(AppButtons.Down);
        Press(AppButtons.Down);
        Press(AppButtons.Enter);
        Assert.AreEqual(AppScreen.Settings, f.Flow.Screen);
    }

    [TestMethod]
    public void SettingsAppliesVolumeFromFirstMenuAndMutePreservesPreference()
    {
        var settings = new UserSettings { MasterVolume = 0.3f, Muted = true };
        var f = new Fixture(settings);
        CollectionAssert.AreEqual(new[] { 0f }, f.Volumes);
        f.Click("settings");
        f.Click("volume", fraction: 0.25f);
        Assert.AreEqual(0.25f, settings.MasterVolume);
        Assert.IsFalse(settings.Muted);
        Assert.AreEqual(0.25f, f.Volumes[^1]);
        f.Send(new(1, 0, false, false, false, default));
        Assert.AreEqual(0.3f, settings.MasterVolume, 1e-6f);
        f.Click("mute");
        Assert.IsTrue(settings.Muted);
        Assert.AreEqual(0f, f.Volumes[^1]);
        Assert.AreEqual(0.3f, settings.MasterVolume, 1e-6f);
        f.Click("mute");
        Assert.AreEqual(0.3f, f.Volumes[^1], 1e-6f);
        f.Click("volume", fraction: 0.99f);
        for (int i = 0; i < 5; i++) f.Send(new(1, 0, false, false, false, default));
        Assert.AreEqual(1f, settings.MasterVolume);
        for (int i = 0; i < 25; i++) f.Send(new(-1, 0, false, false, false, default));
        Assert.AreEqual(0f, settings.MasterVolume);
        Assert.IsTrue(f.View().Texts.Any(text => text.Text == "Master volume: 0%"));
    }

    [TestMethod]
    public void SettingsFromPausePreservesSessionAndAudioPauseUntilExplicitContinue()
    {
        var f = new Fixture();
        f.Click("play");
        f.Flow.Step(default);
        var before = f.Flow.Snapshot;
        f.Send(pause: true);
        f.Click("settings");
        for (int i = 0; i < 10; i++) f.Flow.Step(SessionFixtures.Right);
        f.Click("mute");
        f.Click("back");
        Assert.AreEqual(AppScreen.Paused, f.Flow.Screen);
        Assert.AreSame(before, f.Flow.Snapshot);
        CollectionAssert.AreEqual(new[] { false, true }, f.AudioPause);
        f.Send(new(0, 0, false, true, false, default));
        Assert.AreEqual(AppScreen.Playing, f.Flow.Screen);
        Assert.IsFalse(f.AudioPause[^1]);
        Assert.AreSame(before, f.Flow.Snapshot);
    }

    [TestMethod]
    public void UnfocusedInputCannotActivateControlsAndFocusGainDoesNotContinue()
    {
        var f = new Fixture();
        f.Click("play");
        var before = f.Flow.Snapshot;
        f.Send(new(0, 0, true, false, true, new(500f, 250f)), restart: true, focused: false);
        Assert.AreEqual(AppScreen.Paused, f.Flow.Screen);
        Assert.AreSame(before, f.Flow.Snapshot);
        int resets = f.Resets;
        f.Send(focused: false);
        Assert.AreEqual(resets, f.Resets);
        f.Send(); // Mapper на focus gain не выдаёт фронт удержанного Enter.
        Assert.AreEqual(AppScreen.Paused, f.Flow.Screen);
        Assert.IsFalse(f.View().Controls.Any(control => control.Pressed));
        f.Click("continue");
        Assert.AreEqual(AppScreen.Playing, f.Flow.Screen);
    }

    [TestMethod]
    public void SecondLevelChoiceStartsNewRouteAndNeverInventsFirstLevelCoins()
    {
        var f = new Fixture();
        f.Click("levels");
        Assert.HasCount(2, f.View().Controls.Where(control => control.Kind == MenuControlKind.Level).ToArray());
        Assert.IsTrue(f.View().Texts.Any(text => text.Text.Contains("Training Track")));
        Assert.IsTrue(f.View().Texts.Any(text => text.Text.Contains("Turning Maze")));
        f.Click("level:level2");
        Assert.AreEqual(AppScreen.Playing, f.Flow.Screen);
        Assert.AreEqual("level2", f.Flow.Snapshot!.LevelId);
        f.Finish();
        f.Click("next");
        Assert.AreEqual(AppScreen.GameComplete, f.Flow.Screen);
        Assert.AreEqual(1, f.Flow.TotalCoinCount);
        Assert.HasCount(1, f.Flow.CompletedLevels);
        Assert.IsTrue(f.View().Texts.Any(text => text.Text == "Total coins: 1"));
        f.Click("levels");
        f.Click("back");
        Assert.AreEqual(AppScreen.GameComplete, f.Flow.Screen);
        f.Click("levels");
        f.Click("level:level1");
        Assert.AreEqual(0, f.Flow.TotalCoinCount);
        Assert.IsEmpty(f.Flow.CompletedLevels);
    }

    [TestMethod]
    public void SkinSamplesHaveRealColorsAndSelectionPersistsAcrossRestartAndNext()
    {
        var f = new Fixture();
        f.Click("skins");
        foreach (var skin in GameSkins.All)
            Assert.IsTrue(MenuOverlayAdapter.Build(f.View()).Images.ToArray()
                .Any(image => ReferenceEquals(image.Texture, skin.Preview)));
        f.Click("skin:orange");
        Assert.AreEqual("orange", f.Flow.UserSettings.SkinId);
        f.Click("back");
        f.Click("play");
        var radius = f.Flow.Snapshot!.Ball.Radius;
        Assert.AreSame(GameSkins.Get("orange").Material, f.Flow.Presenter!.BallObject.Material);
        f.Send(restart: true);
        Assert.AreEqual(radius, f.Flow.Snapshot!.Ball.Radius);
        f.Finish();
        f.Click("next");
        Assert.AreSame(GameSkins.Get("orange").Material, f.Flow.Presenter!.BallObject.Material);
        Assert.AreEqual("orange", f.Flow.UserSettings.SkinId);
    }

    [TestMethod]
    public void RetryAndNextUseRealSessionResultsWithoutDoubleCounting()
    {
        var f = new Fixture();
        f.Click("play");
        f.Finish();
        Assert.IsTrue(f.View().Texts.Any(text => text.Text == "Coins 1/1"));
        f.Click("retry", enter: true);
        Assert.AreEqual(0, f.Flow.TotalCoinCount);
        f.Finish();
        f.Click("next", enter: true);
        Assert.AreEqual("level2", f.Flow.Snapshot!.LevelId);
        f.Finish();
        f.Click("retry");
        Assert.AreEqual(1, f.Flow.TotalCoinCount);
        f.Finish();
        f.Click("next");
        Assert.AreEqual(AppScreen.GameComplete, f.Flow.Screen);
        Assert.AreEqual(2, f.Flow.TotalCoinCount);
        Assert.HasCount(2, f.Flow.CompletedLevels);
    }

    [TestMethod]
    public void FailedLevelSelectBackAndRRetryPreserveTheirTransitionRules()
    {
        var f = new Fixture();
        f.Click("play");
        SessionFixtures.Until(() => f.Flow.Screen == AppScreen.Failed,
            () => f.Flow.Step(new(-Vector2.UnitX, false, false)));
        var failed = f.Flow.Snapshot;
        f.Click("levels");
        f.Click("back");
        Assert.AreEqual(AppScreen.Failed, f.Flow.Screen);
        Assert.AreSame(failed, f.Flow.Snapshot);
        int resets = f.Resets;
        var result = f.Send(new(0, 0, true, false, false, default), restart: true);
        Assert.IsTrue(result.ResetCamera);
        Assert.AreEqual(AppScreen.Playing, f.Flow.Screen);
        Assert.AreEqual(0, f.Flow.Snapshot!.Tick);
        Assert.AreNotEqual(failed!.SessionId, f.Flow.Snapshot.SessionId);
        Assert.AreEqual(resets + 1, f.Resets);
    }

    [TestMethod]
    public void ViewRetainsOldLabelsAndReadOnlyCollectionsAfterChanges()
    {
        var f = new Fixture();
        f.Click("settings");
        var before = f.View();
        f.Click("mute");
        Assert.IsTrue(before.Controls.Any(control => control.Label == "Mute: OFF"));
        Assert.IsTrue(f.View().Controls.Any(control => control.Label == "Mute: ON"));
        Assert.ThrowsExactly<NotSupportedException>(() => ((IList<MenuControl>)before.Controls).Clear());
        Assert.ThrowsExactly<NotSupportedException>(() => ((IList<MenuText>)before.Texts).Clear());
        Assert.ThrowsExactly<NotSupportedException>(() => ((IList<MenuPanel>)before.Panels).Clear());
    }

    [TestMethod]
    public void PressedFeedbackLastsOneInputFrameWithoutRepeatingTheAction()
    {
        var f = new Fixture();
        f.Click("settings");
        f.Click("mute");
        Assert.IsTrue(f.View().Controls.Single(control => control.Id == "mute").Pressed);
        f.Send();
        Assert.IsFalse(f.View().Controls.Any(control => control.Pressed));
        Assert.IsTrue(f.Flow.UserSettings.Muted);
        Assert.HasCount(2, f.Volumes);
    }

    [TestMethod]
    public void InvalidOrMinimizedViewportCannotStartOrMutateTheMenu()
    {
        var f = new Fixture();
        var activate = new InputFrame(default, false, false, new(0, 0, true, false, false, default));
        foreach (float size in new[] { -1f, float.NaN, float.PositiveInfinity })
        {
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => f.Menu.Handle(activate, size, Height));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => f.Menu.BuildView(Width, size));
        }
        f.Menu.Handle(activate, 0f, Height);
        Assert.IsEmpty(f.Menu.BuildView(0f, Height).Controls);
        Assert.IsEmpty(f.Menu.BuildView(Width, 0f).Texts);
        Assert.AreEqual(AppScreen.MainMenu, f.Flow.Screen);
        Assert.IsNull(f.Flow.Snapshot);
        f.Send(new(0, 0, false, false, true, new(float.NaN, 10f)));
        Assert.IsNull(f.Flow.Snapshot);
    }

    [TestMethod]
    public void MenuTransitionsClearHeldActivationBeforeTheNextScreen()
    {
        var catalog = new LevelCatalog([SessionFixtures.Level()]);
        var router = new InputRouter();
        var current = new AppInputSnapshot(true);
        var flow = new LevelFlow(catalog, SimulationSettings.Default, new(), () => router.Clear(current));
        var menu = new MenuController(flow, catalog);
        void Send(AppInputSnapshot input)
        {
            current = input;
            menu.Handle(router.Map(input, flow.Screen, 0f), Width, Height, input.IsFocused);
        }
        Send(new(true, AppButtons.Down, AppButtons.Down));
        Send(new(true));
        var enter = new AppInputSnapshot(true, AppButtons.Enter, AppButtons.Enter);
        Send(enter);
        Assert.AreEqual(AppScreen.LevelSelect, flow.Screen);
        Send(enter);
        Assert.IsFalse(menu.BuildView(Width, Height).Controls.Any(control => control.Pressed));
        Send(new(false));
        Send(enter);
        Assert.IsFalse(menu.BuildView(Width, Height).Controls.Any(control => control.Pressed));
        Send(new(true));
        Send(enter);
        Assert.AreEqual(AppScreen.Playing, flow.Screen);
        Assert.AreEqual("level1", flow.Snapshot!.LevelId);
    }
}
