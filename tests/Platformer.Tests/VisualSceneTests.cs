using System.Numerics;
using Engine3D.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Platformer.App;
using Platformer.Core;

namespace Platformer.Tests;

[TestClass]
public sealed class VisualSceneTests
{
    [TestMethod]
    public void TurningMazeJunctionHasExactlyOneCheckerSurfaceAtEachSharedPoint()
    {
        var presenter = new GameScenePresenter(TurningMaze.Create(), visual: true);
        var roads = presenter.Scene.Objects.Where(item => item.Material.Texture is { Width: 128, Height: 64 } or { Width: 8, Height: 128 }).ToArray();
        foreach (var point in new[] { new Vector2(10.37f, 7.23f), new(10.66f, 7.67f), new(10.17f, 7.04f) })
        {
            int covering = 0;
            foreach (var road in roads)
            {
                var vertices = road.Mesh.Vertices.ToArray();
                var indices = road.Mesh.Indices.ToArray();
                var matrix = road.Transform.GetModelMatrix();
                for (int i = 0; i < indices.Length; i += 3)
                {
                    Vector3 a = Vector3.Transform(vertices[indices[i]].Position, matrix);
                    Vector3 b = Vector3.Transform(vertices[indices[i + 1]].Position, matrix);
                    Vector3 c = Vector3.Transform(vertices[indices[i + 2]].Position, matrix);
                    if (MathF.Abs(a.Y) > 0.01f || MathF.Abs(a.Y - b.Y) > 1e-5f || MathF.Abs(a.Y - c.Y) > 1e-5f) continue;
                    Vector2 p = new(a.X, a.Z), u = new(b.X - a.X, b.Z - a.Z), v = new(c.X - a.X, c.Z - a.Z);
                    float determinant = Cross(u, v);
                    if (MathF.Abs(determinant) < 1e-8f) continue;
                    float x = Cross(point - p, v) / determinant, y = Cross(u, point - p) / determinant;
                    if (x > 1e-5f && y > 1e-5f && x + y < 1 - 1e-5f) covering++;
                }
            }
            Assert.AreEqual(1, covering, $"Shared junction at {point} must not contain coplanar duplicates");
        }
        static float Cross(Vector2 a, Vector2 b) => a.X * b.Y - a.Y * b.X;
    }

    [TestMethod]
    public void VisualModeLeavesTheActualSessionAndRouteUnchanged()
    {
        var level = TrainingTrack.Create();
        var catalog = new LevelCatalog([level]);
        var normal = new LevelFlow(catalog, SimulationSettings.Default, new UserSettings());
        var visual = new LevelFlow(catalog, SimulationSettings.Default, new UserSettings(), visual: true);
        normal.Start(level.Id);
        visual.Start(level.Id);
        Assert.IsGreaterThan(normal.Presenter!.Scene.Objects.Count, visual.Presenter!.Scene.Objects.Count);
        for (int tick = 0; tick < 240; tick++)
        {
            var command = new GameCommand(Vector2.UnitX, tick == 60, tick == 90);
            normal.Step(command);
            visual.Step(command);
            Assert.AreEqual(normal.Screen, visual.Screen);
            Assert.AreEqual(normal.Snapshot!.Ball, visual.Snapshot!.Ball);
            Assert.AreEqual(normal.Snapshot.Tick, visual.Snapshot.Tick);
            Assert.AreEqual(normal.Snapshot.SimulationSeconds, visual.Snapshot.SimulationSeconds);
            CollectionAssert.AreEqual(normal.Snapshot.CollectedCoinIds.ToArray(), visual.Snapshot.CollectedCoinIds.ToArray());
            CollectionAssert.AreEqual(normal.DrainEvents().Select(e => (e.Sequence, e.Tick, e.Kind, e.EntityId)).ToArray(),
                visual.DrainEvents().Select(e => (e.Sequence, e.Tick, e.Kind, e.EntityId)).ToArray());
        }
        normal.Restart();
        visual.Restart();
        Assert.AreEqual(normal.Snapshot!.Ball, visual.Snapshot!.Ball);
        Assert.AreEqual(0, visual.Snapshot.Tick);
    }

    [TestMethod]
    public void VisualCoinsDisappearAndRestartRestoresThemWithoutDuplicatingScenery()
    {
        var level = TrainingTrack.Create();
        var session = new GameSession(level, SimulationSettings.Default);
        var presenter = new GameScenePresenter(level, visual: true);
        presenter.Apply(session.Snapshot, GameSkins.All[0]);
        var all = presenter.Scene.Objects.ToArray();
        var initialPositions = all.Select(item => item.Transform.Position).ToArray();
        var first = level.Coins[0];
        var coin = presenter.CoinObjects[first.Id];
        Assert.IsTrue(coin.Mesh.Vertices.ToArray().All(v => v.Position.Length() <= 1f),
            "Coin and star geometry must stay inside the unchanged spherical trigger");
        for (int retry = 0; retry < 10; retry++)
        {
            var initial = session.Snapshot;
            presenter.Apply(initial, GameSkins.All[retry % 2]);
            presenter.Apply(new(initial.SessionId, level.Id, 1, 1d / 120d, SessionPhase.Running,
                initial.Ball, [first.Id], []), GameSkins.All[retry % 2]);
            Assert.IsFalse(presenter.Scene.Objects.Contains(coin));
            Assert.HasCount(all.Length - 1, presenter.Scene.Objects);
            session.Restart();
        }
        presenter.Apply(session.Snapshot, GameSkins.All[0]);
        CollectionAssert.AreEquivalent(all, presenter.Scene.Objects.ToArray());
        CollectionAssert.AreEqual(initialPositions, all.Select(item => item.Transform.Position).ToArray(),
            "Restart must restore the phase of decorative balloons as well as gameplay objects");
        Assert.AreSame(coin, presenter.CoinObjects[first.Id]);
        var second = new GameScenePresenter(level, visual: true);
        second.Apply(session.Snapshot, GameSkins.All[0]);
        CollectionAssert.AreEquivalent(all.Select(item => item.Mesh).Distinct().ToArray(),
            second.Scene.Objects.Select(item => item.Mesh).Distinct().ToArray(), "Restart/new presenter must reuse immutable meshes");
        CollectionAssert.AreEquivalent(all.Select(item => item.Material.Texture).Where(t => t is not null).Distinct().ToArray(),
            second.Scene.Objects.Select(item => item.Material.Texture).Where(t => t is not null).Distinct().ToArray(),
            "Distant palette textures must also be reused by the next presenter");
        foreach (var item in all)
        {
            item.Material.Validate();
            item.Transform.GetModelMatrix();
        }
    }

    [TestMethod]
    public void ContactDarkeningClipsToTheActualFloorRampAndBridgeAndReusesResourcesAfterReset()
    {
        var level = TrainingTrack.Create();
        var presenter = new GameScenePresenter(level, visual: true);
        var scenery = presenter.Scene.Objects.ToHashSet();
        var seen = new Dictionary<(string Surface, Vector2 Offset), (MeshData Mesh, TextureData? Texture)>();
        Guid session = Guid.NewGuid();
        long tick = 0;
        for (int retry = 0; retry < 3; retry++)
        {
            foreach (var surface in level.Surfaces.Where(s => s.Id is "start" or "ramp" or "bridge"))
            foreach (var offset in new[] { Vector2.Zero, new Vector2(-0.85f, -0.85f), new(-0.85f, 0.85f), new(0.85f, -0.85f), new(0.85f, 0.85f) })
            {
                var bounds = surface.Bounds;
                Vector3 normal = Vector3.Transform(Vector3.UnitY, bounds.Orientation);
                Vector3 foot = bounds.ToWorld(new(offset.X * bounds.HalfExtents.X, bounds.HalfExtents.Y,
                    offset.Y * bounds.HalfExtents.Z));
                var ball = new BallState(foot + normal * 0.501f, Vector3.Zero, 0.5f, true, normal, 0, 0);
                Apply(ball);
                var shadow = presenter.Scene.Objects.Except(scenery).Single();
                foreach (var vertex in shadow.Mesh.Vertices)
                {
                    Vector3 local = bounds.ToLocal(Vector3.Transform(vertex.Position, shadow.Transform.GetModelMatrix()));
                    Assert.IsLessThanOrEqualTo(bounds.HalfExtents.X + 0.0001f, MathF.Abs(local.X));
                    Assert.IsLessThanOrEqualTo(bounds.HalfExtents.Z + 0.0001f, MathF.Abs(local.Z));
                    Assert.AreEqual(bounds.HalfExtents.Y + 0.004f, local.Y, 0.0001f);
                }
                if (seen.TryGetValue((surface.Id, offset), out var previous))
                {
                    Assert.AreSame(previous.Mesh, shadow.Mesh);
                    Assert.AreSame(previous.Texture, shadow.Material.Texture);
                }
                else seen.Add((surface.Id, offset), (shadow.Mesh, shadow.Material.Texture));
                Apply(ball with { Grounded = false, GroundNormal = Vector3.Zero });
                CollectionAssert.AreEquivalent(scenery.ToArray(), presenter.Scene.Objects.ToArray(),
                    "A jump removes the contact layer without leaving stale objects");
            }
            session = Guid.NewGuid();
            tick = 0;
        }

        void Apply(BallState ball)
        {
            long currentTick = tick++;
            presenter.Apply(new(session, level.Id, currentTick, currentTick / 120d,
                SessionPhase.Running, ball, [], []), GameSkins.All[0]);
        }
    }

    [TestMethod]
    public void HostKeepsVisualModeThroughMenusPauseAndRestart()
    {
        var level = TrainingTrack.Create();
        var host = new GameplayHost(new LevelCatalog([level]), SimulationSettings.Default, new UserSettings(), visual: true);
        var originalView = host.View;
        var standard = MenuOverlayAdapter.Build(originalView);
        var themed = MenuOverlayAdapter.Build(originalView, visual: true);
        CollectionAssert.AreEqual(standard.Panels.ToArray().Select(p => p.Bounds).ToArray(),
            themed.Images.ToArray().Take(originalView.Panels.Count).Select(p => p.Bounds).ToArray(),
            "Rounded backgrounds must preserve the logical bounds of every source panel");
        Assert.AreEqual(standard.Texts.Length, themed.Texts.Length);
        host.Flow.Start(level.Id);
        host.Advance(new(true), 0d);
        var presenter = host.Flow.Presenter!;
        int objectCount = host.Scene.Objects.Count;
        Assert.AreEqual(presenter.Scene.Objects.Count, objectCount);
        Assert.AreEqual(presenter.Scene.Lighting.AmbientColor, host.Scene.Lighting.AmbientColor);
        Assert.AreEqual(presenter.Scene.Lighting.DirectionalColor, host.Scene.Lighting.DirectionalColor);
        host.Flow.Pause();
        host.Flow.Restart();
        host.Advance(new(true), 0d);
        Assert.AreSame(presenter, host.Flow.Presenter);
        Assert.HasCount(objectCount, host.Scene.Objects);
    }
}
