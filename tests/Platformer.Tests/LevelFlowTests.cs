using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Platformer.App;
using Platformer.Core;

namespace Platformer.Tests;

[TestClass]
public sealed class LevelFlowTests
{
    private static LevelCatalog Catalog() => new([
        SessionFixtures.Level("level1", [new("coin1", new(1f, 0.501f, 0f), 0.15f)], floorHalfWidth: 2f),
        SessionFixtures.Level("level2", [new("coin2", new(1f, 0.501f, 0f), 0.15f)])]);

    private static void Finish(LevelFlow flow) =>
        SessionFixtures.Until(() => flow.Screen == AppScreen.LevelResult, () => flow.Step(SessionFixtures.Right));

    [TestMethod]
    public void RouteStartFailedRetryFinishNextGameCompleteUsesRealSimulationAndPresenter()
    {
        int resets = 0, stops = 0, completions = 0;
        var settings = new UserSettings { MasterVolume = 0.3f, Muted = true, SkinId = "orange" };
        var flow = new LevelFlow(Catalog(), SimulationSettings.Default, settings, () => resets++, () => stops++);
        flow.GameCompleted += () => completions++;
        flow.Start("level1");
        var firstId = flow.Snapshot!.SessionId;
        var presenter = flow.Presenter;
        SessionFixtures.Until(() => flow.Screen == AppScreen.Failed,
            () => flow.Step(new(-Vector2.UnitX, false, false)));
        flow.Restart();
        Assert.AreNotEqual(firstId, flow.Snapshot!.SessionId);
        Assert.AreSame(presenter, flow.Presenter);
        Assert.AreEqual(0, flow.Snapshot.CoinCount);
        Finish(flow);
        Assert.AreEqual(1, flow.Snapshot.CoinCount);
        Assert.IsFalse(flow.Presenter!.Scene.Objects.Contains(flow.Presenter.CoinObjects["coin1"]));
        var oldResult = flow.Snapshot;
        flow.Next();
        Assert.AreEqual("level2", flow.Snapshot!.LevelId);
        Assert.AreNotSame(presenter, flow.Presenter);
        Assert.AreEqual(1, flow.TotalCoinCount);
        Assert.AreEqual(0, flow.Snapshot.CoinCount);
        Finish(flow);
        flow.Next();
        Assert.AreEqual(AppScreen.GameComplete, flow.Screen);
        Assert.AreEqual(2, flow.TotalCoinCount);
        Assert.HasCount(2, flow.CompletedLevels);
        Assert.IsNull(flow.Snapshot);
        Assert.AreEqual(1, completions);
        Assert.ThrowsExactly<InvalidOperationException>(flow.Next);
        Assert.AreEqual(1, completions);
        Assert.AreEqual(1, oldResult.CoinCount);
        Assert.AreSame(settings, flow.UserSettings);
        Assert.AreEqual(0.3f, settings.MasterVolume);
        Assert.IsTrue(settings.Muted);
        Assert.AreEqual("orange", settings.SkinId);
        Assert.AreEqual(resets, stops);
        Assert.IsGreaterThan(5, resets);
    }

    [TestMethod]
    public void RetryClearsCurrentAttemptButPreservesPreviousCompletedLevels()
    {
        var flow = new LevelFlow(Catalog(), SimulationSettings.Default, new UserSettings());
        flow.Start("level1");
        Finish(flow);
        flow.Next();
        var previous = flow.CompletedLevels;
        Finish(flow);
        Assert.AreEqual(2, flow.TotalCoinCount);
        flow.Restart();
        Assert.AreEqual(1, flow.TotalCoinCount);
        Assert.HasCount(1, flow.CompletedLevels);
        Assert.AreEqual(0, flow.Snapshot!.CoinCount);
        Assert.IsEmpty(flow.DrainEvents());
        Finish(flow);
        flow.Next();
        Assert.AreEqual(2, flow.TotalCoinCount);
        Assert.HasCount(1, previous);
        Assert.ThrowsExactly<NotSupportedException>(() => ((IList<LevelResult>)previous).Clear());
    }

    [TestMethod]
    public void DirectSecondLevelAndNewStartDoNotAccumulateEarlierRoutes()
    {
        var flow = new LevelFlow(Catalog(), SimulationSettings.Default, new UserSettings());
        flow.Start("level1");
        Finish(flow);
        flow.Next();
        flow.Start("level2");
        Assert.IsEmpty(flow.CompletedLevels);
        Finish(flow);
        flow.Next();
        Assert.AreEqual(1, flow.TotalCoinCount);
        Assert.AreEqual("level2", flow.CompletedLevels.Single().LevelId);
        flow.Start("level1");
        Assert.AreEqual(0, flow.TotalCoinCount);
        Assert.IsEmpty(flow.CompletedLevels);
    }

    [TestMethod]
    public void PauseFocusLossAndContinueClearTransientInputAndDoNotStepTime()
    {
        var audio = new List<bool>();
        bool queuedJump = true;
        int resets = 0;
        var flow = new LevelFlow(Catalog(), SimulationSettings.Default, new UserSettings(),
            () => { queuedJump = false; resets++; }, setAudioPaused: audio.Add);
        flow.Start("level1");
        queuedJump = true;
        flow.LoseFocus();
        var paused = flow.Snapshot;
        Assert.IsFalse(queuedJump);
        Assert.AreEqual(AppScreen.Paused, flow.Screen);
        for (int i = 0; i < 10; i++) flow.Step(new(Vector2.UnitX, true, true));
        Assert.AreSame(paused, flow.Snapshot);
        Assert.IsEmpty(flow.DrainEvents());
        flow.Pause(); // повторный Pause не дублирует audio-команду
        flow.Continue();
        Assert.AreEqual(AppScreen.Playing, flow.Screen);
        CollectionAssert.AreEqual(new[] { false, true, false }, audio);
        Assert.AreEqual(3, resets);
        flow.Step(default);
        Assert.AreEqual(1, flow.Snapshot!.Tick);
        Assert.IsTrue(flow.Snapshot.Ball.Grounded);
    }

    [TestMethod]
    public void InvalidTransitionAndFailedPreparationPreserveCurrentRoute()
    {
        var flow = new LevelFlow(Catalog(), SimulationSettings.Default, new UserSettings());
        Assert.ThrowsExactly<InvalidOperationException>(flow.Next);
        Assert.ThrowsExactly<InvalidOperationException>(flow.Restart);
        Assert.ThrowsExactly<InvalidOperationException>(flow.Continue);
        flow.Start("level1");
        var before = flow.Snapshot;
        var presenter = flow.Presenter;
        Assert.ThrowsExactly<KeyNotFoundException>(() => flow.Start("missing"));
        Assert.ThrowsExactly<InvalidOperationException>(flow.Next);
        Assert.AreSame(before, flow.Snapshot);
        Assert.AreSame(presenter, flow.Presenter);
        flow.MainMenu();
        Assert.AreEqual(AppScreen.MainMenu, flow.Screen);
        Assert.IsNull(flow.Snapshot);
        Assert.IsNull(flow.Presenter);
    }

    [TestMethod]
    public void UserSettingsRejectInvalidValuesBeforeReplacingValidPreferences()
    {
        var settings = new UserSettings { MasterVolume = 0.3f, SkinId = "orange" };
        foreach (float volume in new[] { float.NaN, float.PositiveInfinity, -1f, 1.1f })
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => settings.MasterVolume = volume);
        Assert.ThrowsExactly<KeyNotFoundException>(() => settings.SkinId = "missing");
        Assert.AreEqual(0.3f, settings.MasterVolume);
        Assert.AreEqual("orange", settings.SkinId);
    }

    [TestMethod]
    public void RestartWhilePausedStopsOldAudioAndClearsItsPauseState()
    {
        bool paused = false;
        int stops = 0;
        var flow = new LevelFlow(Catalog(), SimulationSettings.Default, new UserSettings(),
            stopGameplayAudio: () => stops++, setAudioPaused: value => paused = value);
        flow.Start("level1");
        flow.Pause();
        Assert.IsTrue(paused);
        var id = flow.Snapshot!.SessionId;
        flow.Restart();
        Assert.IsFalse(paused);
        Assert.AreEqual(2, stops);
        Assert.AreEqual(AppScreen.Playing, flow.Screen);
        Assert.AreNotEqual(id, flow.Snapshot!.SessionId);
    }

    [TestMethod]
    public void InvalidHazardBudgetOnNextPreservesCompletedAttemptAndDoesNotResetAdapters()
    {
        var first = SessionFixtures.Level(finishX: 0f);
        var hazard = new HazardDefinition("beam", Vector3.Zero, Vector3.UnitY, Vector3.Zero,
            Vector3.One, Quaternion.Identity, 10000f, 0f);
        var second = new LevelDefinition("level2", first.Spawn, first.KillY, first.Finish, first.Surfaces, [], [hazard]);
        int resets = 0;
        var flow = new LevelFlow(new LevelCatalog([first, second]), SimulationSettings.Default,
            new UserSettings(), () => resets++);
        flow.Start("level1");
        flow.Step(default);
        var before = flow.Snapshot;
        int calls = resets;
        Assert.ThrowsExactly<ArgumentException>(flow.Next);
        Assert.AreSame(before, flow.Snapshot);
        Assert.AreEqual(AppScreen.LevelResult, flow.Screen);
        Assert.IsEmpty(flow.CompletedLevels);
        Assert.AreEqual(calls, resets);
    }

    [TestMethod]
    public void FailedResetCallbackDoesNotPublishNewSessionOrClearRoute()
    {
        bool fail = false;
        var flow = new LevelFlow(Catalog(), SimulationSettings.Default, new UserSettings(),
            () => { if (fail) throw new InvalidOperationException("input reset"); });
        flow.Start("level1");
        var before = flow.Snapshot;
        var presenter = flow.Presenter;
        fail = true;
        Assert.ThrowsExactly<InvalidOperationException>(() => flow.Start("level2"));
        Assert.AreSame(before, flow.Snapshot);
        Assert.AreSame(presenter, flow.Presenter);
        Assert.AreEqual(AppScreen.Playing, flow.Screen);
    }

    [TestMethod]
    public void WallSlideRollMatchesPresenterUsingEverySegmentOfTheRealPhysicsPath()
    {
        var level = new LevelDefinition("wall-slide", new(0f, 0.501f, 0f), -10f,
            new OrientedBox(new(40f, 1f, 40f), Vector3.One, Quaternion.Identity),
            [new("floor", new OrientedBox(new(0f, -0.5f, 0f), new(50f, 0.5f, 50f), Quaternion.Identity)),
             new("wall", new OrientedBox(new(2f, 1f, 0f), new(0.1f, 2f, 50f), Quaternion.Identity))], [], []);
        var settings = new SimulationSettings(fixedStepSeconds: 1f / 30f, moveSpeed: 20f,
            moveAcceleration: 100f, boostMultiplier: 1f, maxFallSpeed: 30f);
        var flow = new LevelFlow(new LevelCatalog([level]), settings, new UserSettings());
        var simulator = new BallSimulator(level, settings);
        var ball = simulator.CreateInitialState();
        var reference = new GameScenePresenter(level);
        var skin = GameSkins.Get("classic-blue");
        flow.Start(level.Id);
        reference.Apply(flow.Snapshot!, skin);
        var command = new GameCommand(Vector2.Normalize(Vector2.One), false, false);
        bool sawWallSlide = false;
        for (int tick = 1; tick <= 60; tick++)
        {
            var result = simulator.Step(ball, command);
            ball = result.Ball;
            flow.Step(command);
            Assert.AreEqual(ball, flow.Snapshot!.Ball, $"Physics must match at tick {tick}");
            reference.Apply(flow.Snapshot, skin, result.Path);
            sawWallSlide |= result.Path.Any(segment => segment.End.X > segment.Start.X)
                && result.Path.Any(segment => segment.End.X == segment.Start.X && segment.End.Z > segment.Start.Z);
            Assert.AreEqual(1f, MathF.Abs(Quaternion.Dot(reference.BallObject.Transform.Rotation,
                flow.Presenter!.BallObject.Transform.Rotation)), 1e-6f, $"Rolling must follow Path at tick {tick}");
        }
        Assert.IsTrue(sawWallSlide, "The fixture must include movement into the wall followed by sliding");
    }
}
