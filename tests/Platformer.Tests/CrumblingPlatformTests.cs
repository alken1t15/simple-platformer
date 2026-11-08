using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Platformer.App;
using Platformer.Core;

namespace Platformer.Tests;

[TestClass]
public sealed class CrumblingPlatformTests
{
    private static LevelDefinition Level() => new("crumble-test", new(0, 0.501f, 0), -5,
        new(new(10, 0.75f, 0), new(0.1f, 0.75f, 1), Quaternion.Identity),
        [new("fragile", new(new(0, -0.2f, 0), new(2, 0.2f, 2), Quaternion.Identity), 0.2f)], [], []);

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ContactStartsWarningThenRemovesRealSupportAndRestartRestoresIt(bool visual)
    {
        var level = Level();
        var session = new GameSession(level, SimulationSettings.Default);
        var presenter = new GameScenePresenter(level, visual);
        presenter.Apply(session.Snapshot, GameSkins.All[0]);
        var body = presenter.SurfaceObjects["fragile"];
        var readyMaterial = body.Material;
        session.Step(default);
        Assert.IsNotNull(session.Snapshot.Platforms[0].TriggeredAtSeconds);
        presenter.Apply(session.Snapshot, GameSkins.All[0], session.LastPath);
        Assert.AreNotSame(readyMaterial, body.Material);
        double deadline = session.Snapshot.Platforms[0].TriggeredAtSeconds!.Value + 0.2f;
        while ((session.Snapshot.Tick + 1) * (double)SimulationSettings.Default.FixedStepSeconds < deadline)
            session.Step(default);
        session.DrainEvents();
        session.Step(new(Vector2.Zero, true, false)); // Опора исчезла: прыжка от воздуха быть не должно.
        Assert.IsTrue(session.Snapshot.Platforms[0].Collapsed);
        Assert.IsFalse(session.Snapshot.Ball.Grounded);
        Assert.IsLessThan(0f, session.Snapshot.Ball.Velocity.Y);
        Assert.IsFalse(session.DrainEvents().Any(item => item.Kind == GameEventKind.Jumped));
        presenter.Apply(session.Snapshot, GameSkins.All[0]);
        Assert.IsFalse(presenter.Scene.Objects.Contains(body));
        for (int i = 0; i < 240 && session.Snapshot.Phase == SessionPhase.Running; i++) session.Step(default);
        Assert.AreEqual(SessionPhase.Failed, session.Snapshot.Phase);
        session.Restart();
        presenter.Apply(session.Snapshot, GameSkins.All[0]);
        Assert.IsTrue(presenter.Scene.Objects.Contains(body));
        Assert.AreSame(readyMaterial, body.Material);
        Assert.IsNull(session.Snapshot.Platforms[0].TriggeredAtSeconds);
        Assert.IsFalse(session.Snapshot.Platforms[0].Collapsed);
        Assert.IsTrue(session.Snapshot.Ball.Grounded);
    }

    [TestMethod]
    public void PauseFreezesTimerAndFailedStepPublishesNeitherTimerNorPathNorEvents()
    {
        var level = Level();
        var flow = new LevelFlow(new([level]), SimulationSettings.Default, new UserSettings());
        flow.Start(level.Id); flow.Step(default); flow.Pause();
        var paused = flow.Snapshot;
        for (int i = 0; i < 500; i++) flow.Step(default);
        Assert.AreSame(paused, flow.Snapshot);
        flow.Continue(); flow.Step(default);
        Assert.IsFalse(flow.Snapshot!.Platforms[0].Collapsed);

        var simulator = new BallSimulator(level, SimulationSettings.Default);
        bool fail = false;
        var session = new GameSession(level, SimulationSettings.Default, (ball, command) =>
            fail ? throw new InvalidOperationException("test failure") : simulator.Step(ball, command));
        session.Step(default);
        double deadline = session.Snapshot.Platforms[0].TriggeredAtSeconds!.Value + 0.2f;
        while ((session.Snapshot.Tick + 1) * (double)SimulationSettings.Default.FixedStepSeconds < deadline)
            session.Step(default);
        var before = session.Snapshot;
        var path = session.LastPath;
        fail = true;
        Assert.ThrowsExactly<InvalidOperationException>(() => session.Step(default));
        Assert.AreSame(before, session.Snapshot);
        Assert.AreSame(path, session.LastPath);
        Assert.IsEmpty(session.DrainEvents());
    }

    [TestMethod]
    public void CameraIgnoresCollapsedSupportAndSnapshotOwnsItsPlatformArray()
    {
        var level = Level();
        var session = new GameSession(level, SimulationSettings.Default);
        var before = session.Snapshot;
        CrumblingPlatformState[] states = [new("fragile", 0, true)];
        var snapshot = new SessionSnapshot(before.SessionId, level.Id, 1, 0.25, before.Phase,
            before.Ball, [], [], states);
        states[0] = new("changed", null, false);
        Assert.AreEqual("fragile", snapshot.Platforms[0].Id);
        var rig = new CameraRig();
        var target = new Vector3(0, 0, 0);
        rig.Update(Vector2.Zero, target, level, 0.1f);
        Assert.IsTrue(rig.TargetObstructed);
        rig.Update(Vector2.Zero, target, level, 1f, snapshot.Platforms);
        Assert.IsFalse(rig.TargetObstructed);
        Assert.IsGreaterThan(5.9f, rig.Distance);
    }
}
