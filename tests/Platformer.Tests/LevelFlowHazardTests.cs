using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Platformer.App;
using Platformer.Core;
using static Platformer.Tests.SessionFixtures;

namespace Platformer.Tests;

[TestClass]
public sealed class LevelFlowHazardTests
{
    private static void AssertPresentedHazards(LevelFlow flow)
    {
        foreach (var pose in flow.Snapshot!.Hazards)
        {
            var transform = flow.Presenter!.HazardObjects[pose.Id].Transform;
            LevelDefinitionTests.Near(pose.Bounds.Center, transform.Position);
            LevelDefinitionTests.Near(pose.Bounds.HalfExtents * 2f, transform.Scale);
            Assert.AreEqual(1f, MathF.Abs(Quaternion.Dot(pose.Bounds.Orientation, transform.Rotation)), 1e-6f);
        }
    }

    [TestMethod]
    public void RealHazardContactPauseAndRestartKeepPresenterAndTransitionCallbacksInSync()
    {
        var settings = SimulationSettings.Default;
        var level = RotatingContactLevel(settings);
        int resets = 0, stops = 0;
        var audio = new List<bool>();
        var flow = new LevelFlow(new LevelCatalog([level]), settings, new UserSettings(),
            () => resets++, () => stops++, audio.Add);
        flow.Start(level.Id);
        var initial = flow.Snapshot!;
        var presenter = flow.Presenter;
        var scene = presenter!.Scene;
        AssertPresentedHazards(flow);
        flow.Step(default);
        Assert.AreEqual(AppScreen.Playing, flow.Screen);
        AssertPresentedHazards(flow);
        flow.LoseFocus();
        var paused = flow.Snapshot;
        for (int i = 0; i < 10; i++) flow.Step(SessionFixtures.Right);
        Assert.AreSame(paused, flow.Snapshot);
        AssertPresentedHazards(flow);
        Assert.IsEmpty(flow.DrainEvents());
        flow.Continue();
        flow.Step(default);
        Assert.AreEqual(AppScreen.Failed, flow.Screen);
        Assert.AreEqual(2, flow.Snapshot!.Tick);
        AssertPresentedHazards(flow);
        Assert.AreEqual(4, resets);
        Assert.AreEqual(2, stops);
        CollectionAssert.AreEqual(new[] { false, true, false, false }, audio);
        var failed = flow.Snapshot;
        for (int i = 0; i < 10; i++) flow.Step(default);
        Assert.AreSame(failed, flow.Snapshot);
        Assert.AreEqual(4, resets);
        Assert.AreEqual(GameEventKind.Failed, flow.DrainEvents().Single().Kind);
        Assert.IsEmpty(flow.DrainEvents());
        flow.Restart();
        Assert.AreEqual(AppScreen.Playing, flow.Screen);
        Assert.AreNotEqual(initial.SessionId, flow.Snapshot!.SessionId);
        Assert.AreEqual(0, flow.Snapshot.Tick);
        Assert.AreEqual(0d, flow.Snapshot.SimulationSeconds);
        Assert.AreEqual(initial.Ball, flow.Snapshot.Ball);
        Assert.AreSame(presenter, flow.Presenter);
        Assert.AreSame(scene, flow.Presenter!.Scene);
        AssertPose(initial.Hazards.Single(), flow.Snapshot.Hazards.Single());
        AssertPresentedHazards(flow);
        Assert.AreEqual(5, resets);
        Assert.AreEqual(3, stops);
        Assert.IsEmpty(flow.DrainEvents());
        Assert.AreEqual(2, failed.Tick);
    }

    [TestMethod]
    public void NextStartsAHazardLevelWithItsInitialPoseAndPreservesTheCompletedResult()
    {
        var first = SessionFixtures.Level(finishX: 0f, coins: [new("coin", new(0f, 0.501f, 0f), 0.1f)]);
        var second = WithHazards(SessionFixtures.Level("level2", finishX: 50f),
            Beam(pivot: new(10f, 1f, 0f), speed: 2f, angle: 0.4f, offset: Vector3.UnitX));
        var flow = new LevelFlow(new LevelCatalog([first, second]), SimulationSettings.Default, new UserSettings());
        flow.Start(first.Id);
        flow.Step(default);
        var completed = flow.Snapshot!;
        var presenter = flow.Presenter;
        flow.Next();
        Assert.AreEqual(AppScreen.Playing, flow.Screen);
        Assert.AreEqual(second.Id, flow.Snapshot!.LevelId);
        Assert.AreNotEqual(completed.SessionId, flow.Snapshot.SessionId);
        Assert.AreEqual(0d, flow.Snapshot.SimulationSeconds);
        Assert.AreNotSame(presenter, flow.Presenter);
        Assert.AreEqual(1, flow.TotalCoinCount);
        Assert.AreEqual(new LevelResult(first.Id, 1, 1), flow.CompletedLevels.Single());
        Assert.IsEmpty(flow.DrainEvents());
        AssertPose(HazardMotion.Evaluate(second.Hazards.Single(), 0d), flow.Snapshot.Hazards.Single());
        AssertPresentedHazards(flow);
        flow.Step(default);
        Assert.AreEqual(AppScreen.Playing, flow.Screen);
        AssertPresentedHazards(flow);
    }

    [TestMethod]
    public void RealHazardBudgetErrorPreservesTheSceneScreenAndUnreadEvents()
    {
        var settings = new SimulationSettings(moveSpeed: 0.1f, moveAcceleration: 100f,
            jumpSpeed: 0.1f, boostMultiplier: 1f, maxFallSpeed: 0.1f, maxCollisionSubsteps: 1);
        var basis = SessionFixtures.Level(finishX: 50f);
        var spawn = new Vector3(2.51f, 0.501f, 0f);
        var level = new LevelDefinition("budget", spawn, basis.KillY, basis.Finish, basis.Surfaces,
            [new("coin", spawn, 0.1f)], [Beam(speed: 1f)]);
        int resets = 0;
        var flow = new LevelFlow(new LevelCatalog([level]), settings, new UserSettings(), () => resets++);
        flow.Start(level.Id);
        flow.Step(default);
        InvalidOperationException? error = null;
        for (int i = 0; i < 20; i++)
        {
            var before = flow.Snapshot;
            var rotation = flow.Presenter!.HazardObjects["beam"].Transform.Rotation;
            var ball = flow.Presenter.BallObject.Transform.Position;
            try { flow.Step(new(-Vector2.UnitX, false, false)); }
            catch (InvalidOperationException exception)
            {
                error = exception;
                Assert.AreSame(before, flow.Snapshot);
                Assert.AreEqual(rotation, flow.Presenter.HazardObjects["beam"].Transform.Rotation);
                Assert.AreEqual(ball, flow.Presenter.BallObject.Transform.Position);
                break;
            }
        }
        Assert.IsNotNull(error, "The near miss must exhaust the real one-substep hazard query budget");
        StringAssert.Contains(error.Message, "beam");
        Assert.AreEqual(AppScreen.Playing, flow.Screen);
        Assert.AreEqual(1, resets);
        AssertPresentedHazards(flow);
        var coin = flow.DrainEvents().Single();
        Assert.AreEqual(GameEventKind.CoinCollected, coin.Kind);
        Assert.AreEqual(1, coin.Sequence);
        Assert.AreEqual(1, flow.Snapshot!.CoinCount);
    }
}
