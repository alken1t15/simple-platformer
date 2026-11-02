using System.Numerics;
using Engine3D.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Platformer.App;
using Platformer.Core;

namespace Platformer.Tests;

[TestClass]
public sealed class GameScenePresenterTests
{
    private static readonly Guid Session = Guid.Parse("07000000-0000-0000-0000-000000000001");
    private static BallState Ball(Vector3 center, bool grounded = true, Vector3? normal = null) =>
        new(center, Vector3.Zero, 0.5f, grounded, normal ?? (grounded ? Vector3.UnitY : Vector3.Zero), 0f, 0f);
    private static SessionSnapshot Snapshot(LevelDefinition level, long tick, BallState? ball = null,
        string[]? coins = null, Guid? session = null, HazardPose[]? hazards = null) =>
        new(session ?? Session, level.Id, tick, tick / 120d, SessionPhase.Running,
            ball ?? Ball(level.Spawn), coins ?? [], hazards ?? []);

    [TestMethod]
    public void SurfaceTransformsMapEveryObbCornerToSameWorldPoint()
    {
        var level = TrainingTrack.Create();
        var presenter = new GameScenePresenter(level);
        foreach (var surface in level.Surfaces)
        {
            var item = presenter.SurfaceObjects[surface.Id];
            Assert.AreSame(GameMeshes.Box, item.Mesh);
            var model = item.Transform.GetModelMatrix();
            foreach (int x in new[] { -1, 1 })
            foreach (int y in new[] { -1, 1 })
            foreach (int z in new[] { -1, 1 })
            {
                var corner = new Vector3(x, y, z);
                LevelDefinitionTests.Near(surface.Bounds.ToWorld(corner * surface.Bounds.HalfExtents),
                    Vector3.Transform(corner * 0.5f, model));
            }
        }
    }

    [TestMethod]
    public void CollectedIdsHideImmediatelyAndRestartRestoresSameObjects()
    {
        var level = TrainingTrack.Create();
        var presenter = new GameScenePresenter(level);
        var coin = presenter.CoinObjects["coin-start-1"];
        int count = presenter.Scene.Objects.Count;
        presenter.Apply(Snapshot(level, 0), GameSkins.All[0]);
        presenter.Apply(Snapshot(level, 1, coins: ["coin-start-1"]), GameSkins.All[0]);
        Assert.IsFalse(presenter.Scene.Objects.Contains(coin));
        Assert.HasCount(count - 1, presenter.Scene.Objects);
        presenter.Apply(Snapshot(level, 1, coins: ["coin-start-1"]), GameSkins.All[1]);
        Assert.HasCount(count - 1, presenter.Scene.Objects);
        presenter.Apply(Snapshot(level, 0, session: Guid.NewGuid()), GameSkins.All[1]);
        Assert.IsTrue(presenter.Scene.Objects.Contains(coin));
        Assert.AreSame(coin, presenter.CoinObjects["coin-start-1"]);
        Assert.HasCount(count, presenter.Scene.Objects);
    }

    [TestMethod]
    public void RollingAngleIsDistanceOverRadiusAndSameTickCannotRollTwice()
    {
        var level = TrainingTrack.Create();
        var presenter = new GameScenePresenter(level);
        presenter.Apply(Snapshot(level, 0, Ball(Vector3.Zero)), GameSkins.All[0]);
        var moved = Snapshot(level, 1, Ball(new Vector3(MathF.PI / 4f, 0f, 0f)));
        presenter.Apply(moved, GameSkins.All[0]);
        LevelDefinitionTests.Near(Vector3.UnitX, Vector3.Transform(Vector3.UnitY, presenter.BallObject.Transform.Rotation));
        var rotation = presenter.BallObject.Transform.Rotation;
        presenter.Apply(moved, GameSkins.All[1]);
        Assert.AreEqual(rotation, presenter.BallObject.Transform.Rotation);
        Assert.AreSame(GameSkins.All[1].Material, presenter.BallObject.Material);
        Assert.AreEqual(moved.Ball.Center, presenter.BallObject.Transform.Position);
        Assert.AreEqual(new Vector3(moved.Ball.Radius), presenter.BallObject.Transform.Scale);
        Assert.AreEqual(0.5f, moved.Ball.Radius);
    }

    [TestMethod]
    public void SuppliedPathAccumulatesTurnsEvenWhenBallReturnsToSamePosition()
    {
        var level = TrainingTrack.Create();
        var presenter = new GameScenePresenter(level);
        presenter.Apply(Snapshot(level, 0, Ball(Vector3.Zero)), GameSkins.All[0]);
        Vector3 a = new(0.5f, 0f, 0f), b = new(0.5f, 0f, 0.5f), c = new(0f, 0f, 0.5f);
        MotionSegment[] path = [new(Vector3.Zero, a, 0f, 0.25f), new(a, b, 0.25f, 0.5f),
            new(b, c, 0.5f, 0.75f), new(c, Vector3.Zero, 0.75f, 1f)];
        presenter.Apply(Snapshot(level, 1, Ball(Vector3.Zero)), GameSkins.All[0], path);
        Assert.IsLessThan(0.999f, MathF.Abs(presenter.BallObject.Transform.Rotation.W));
        Assert.AreEqual(1f, presenter.BallObject.Transform.Rotation.Length(), 1e-6f);
    }

    [TestMethod]
    public void VerticalAndAirborneDisplacementsDoNotProduceGroundRolling()
    {
        var level = TrainingTrack.Create();
        var presenter = new GameScenePresenter(level);
        presenter.Apply(Snapshot(level, 0, Ball(Vector3.Zero)), GameSkins.All[0]);
        presenter.Apply(Snapshot(level, 1, Ball(Vector3.UnitY)), GameSkins.All[0]);
        Assert.AreEqual(Quaternion.Identity, presenter.BallObject.Transform.Rotation);
        presenter.Apply(Snapshot(level, 2, Ball(new Vector3(2f, 2f, 0f), false)), GameSkins.All[0]);
        Assert.AreEqual(Quaternion.Identity, presenter.BallObject.Transform.Rotation);
        presenter.Apply(Snapshot(level, 0, session: Guid.NewGuid()), GameSkins.All[1]);
        Assert.AreEqual(Quaternion.Identity, presenter.BallObject.Transform.Rotation);
    }

    [TestMethod]
    public void PhysicsStepPathCanDrivePresenterOnAnInclineWithoutMutatingCore()
    {
        var level = TrainingTrack.Create();
        var simulator = new BallSimulator(level, SimulationSettings.Default);
        var presenter = new GameScenePresenter(level);
        var state = simulator.CreateInitialState();
        presenter.Apply(Snapshot(level, 0, state), GameSkins.All[0]);
        bool reachedSlope = false;
        for (int tick = 1; tick <= 180; tick++)
        {
            var step = simulator.Step(state, new GameCommand(Vector2.UnitX, false, false));
            var snapshot = Snapshot(level, tick, step.Ball);
            var original = snapshot.Ball;
            presenter.Apply(snapshot, GameSkins.All[tick % 2], step.Path);
            Assert.AreEqual(original, snapshot.Ball);
            Assert.AreEqual(step.Ball.Center, presenter.BallObject.Transform.Position);
            Assert.AreEqual(new Vector3(step.Ball.Radius), presenter.BallObject.Transform.Scale);
            reachedSlope |= step.Ball.Grounded && step.Ball.GroundNormal.X < -0.1f;
            state = step.Ball;
        }
        Assert.IsTrue(reachedSlope);
    }

    [TestMethod]
    public void HazardBoundsComeFromSnapshotAndFinishIsAnOpenFrame()
    {
        var baseLevel = TrainingTrack.Create();
        var hazard = new HazardDefinition("beam", new Vector3(7f, 2f, 10f), Vector3.UnitY,
            Vector3.Zero, new Vector3(2f, 0.15f, 0.15f), Quaternion.Identity, 1f, 0f);
        var level = new LevelDefinition("hazard-level", baseLevel.Spawn, baseLevel.KillY,
            baseLevel.Finish, baseLevel.Surfaces, baseLevel.Coins, [hazard]);
        var pose = new OrientedBox(new Vector3(7f, 2.2f, 10f), hazard.HalfExtents,
            Quaternion.CreateFromAxisAngle(Vector3.UnitY, 0.7f));
        var presenter = new GameScenePresenter(level);
        presenter.Apply(Snapshot(level, 0, hazards: [new("beam", pose)]), GameSkins.All[0]);
        var transform = presenter.HazardObjects["beam"].Transform;
        Assert.AreEqual(pose.Center, transform.Position);
        Assert.AreEqual(pose.Orientation, transform.Rotation);
        Assert.AreEqual(pose.HalfExtents * 2f, transform.Scale);
        Assert.HasCount(3, presenter.FinishObjects);
        foreach (var part in presenter.FinishObjects)
            Assert.IsTrue(part.Transform.Scale.X < 0.1f || part.Transform.Scale.Y < 0.1f);
    }

    [TestMethod]
    public void InvalidSnapshotOrPathLeavesSceneUnchanged()
    {
        var level = TrainingTrack.Create();
        var presenter = new GameScenePresenter(level);
        presenter.Apply(Snapshot(level, 1), GameSkins.All[0]);
        var before = presenter.BallObject.Transform.Position;
        Assert.ThrowsExactly<ArgumentException>(() => presenter.Apply(Snapshot(level, 0), GameSkins.All[0]));
        Assert.ThrowsExactly<ArgumentException>(() => presenter.Apply(Snapshot(level, 1,
            Ball(level.Spawn + Vector3.UnitX)), GameSkins.All[0]));
        Assert.ThrowsExactly<ArgumentException>(() => presenter.Apply(Snapshot(level, 2,
            Ball(level.Spawn) with { Radius = 0.8f }), GameSkins.All[0]));
        Assert.ThrowsExactly<ArgumentException>(() => presenter.Apply(Snapshot(level, 2, coins: ["missing"]), GameSkins.All[0]));
        Assert.ThrowsExactly<ArgumentException>(() => presenter.Apply(Snapshot(level, 2,
            hazards: [new("missing", level.Finish)]), GameSkins.All[0]));
        Assert.ThrowsExactly<ArgumentException>(() => presenter.Apply(Snapshot(level, 2), GameSkins.All[0], []));
        Assert.ThrowsExactly<ArgumentException>(() => presenter.Apply(Snapshot(level, 2), GameSkins.All[0],
            [new(before, before, 0.5f, 1f)]));
        Assert.ThrowsExactly<ArgumentException>(() => presenter.Apply(Snapshot(level, 2), GameSkins.All[0],
            [new(before, new Vector3(float.MaxValue), 0f, 0.5f),
                new(new Vector3(float.MaxValue), before, 0.5f, 1f)]));
        Assert.AreEqual(before, presenter.BallObject.Transform.Position);
        Assert.AreEqual(Quaternion.Identity, presenter.BallObject.Transform.Rotation);
        Assert.AreSame(GameSkins.All[0].Material, presenter.BallObject.Material);
    }

    [TestMethod]
    public void RepeatedApplyRestartAndNewPresentersReuseEveryMeshAndSkinTexture()
    {
        var level = TrainingTrack.Create();
        var presenter = new GameScenePresenter(level);
        var objects = presenter.Scene.Objects.ToArray();
        var meshes = objects.Select(item => item.Mesh).Distinct().ToArray();
        Assert.HasCount(3, meshes);
        for (int restart = 0; restart < 50; restart++)
        {
            Guid session = Guid.NewGuid();
            presenter.Apply(Snapshot(level, 0, session: session), GameSkins.All[restart % 2]);
            presenter.Apply(Snapshot(level, 1, coins: ["coin-start-1"], session: session), GameSkins.All[restart % 2]);
            Assert.AreSame(objects.Single(item => item.Mesh == GameMeshes.Sphere), presenter.BallObject);
        }
        presenter.Apply(Snapshot(level, 0, session: Guid.NewGuid()), GameSkins.All[0]);
        CollectionAssert.AreEquivalent(objects, presenter.Scene.Objects.ToArray());
        var nextLevel = new GameScenePresenter(level);
        Assert.AreSame(presenter.BallObject.Mesh, nextLevel.BallObject.Mesh);
        Assert.AreSame(presenter.CoinObjects.Values.First().Mesh, nextLevel.CoinObjects.Values.First().Mesh);
        Assert.AreSame(GameSkins.All[0].Material.Texture, GameSkins.All[1].Material.Texture);
    }

    [TestMethod]
    public void PreviewExercisesCoinsSkinSwitchAndResetWithoutReplacingSceneObjects()
    {
        var preview = new PresenterPreview();
        var scene = preview.Presenter.Scene;
        var ball = preview.Presenter.BallObject;
        int initialCount = scene.Objects.Count;
        for (int tick = 0; tick < 150; tick++) preview.Advance();
        Assert.HasCount(initialCount - 1, scene.Objects);
        Assert.AreNotEqual(Quaternion.Identity, ball.Transform.Rotation);
        preview.ToggleSkin();
        Assert.AreEqual("orange", preview.Skin.Id);
        preview.Restart();
        Assert.HasCount(initialCount, scene.Objects);
        Assert.AreEqual(Quaternion.Identity, ball.Transform.Rotation);
        Assert.AreSame(scene, preview.Presenter.Scene);
        Assert.AreSame(ball, preview.Presenter.BallObject);
        Assert.AreEqual("orange", preview.Skin.Id);
    }
}
