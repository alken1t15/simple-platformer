using System.Numerics;
using Engine3D.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Platformer.App;
using Platformer.Core;

namespace Platformer.Tests;

[TestClass]
public sealed class WinterCollectibleTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void WinterGiftsShareAssetsCollectAsWholeObjectsAndReturnOnRestart(bool visual)
    {
        var catalog = GameLevels.CreateCatalog();
        MeshData? mesh = null;
        foreach (var level in catalog.Levels.Where(level => GameChapters.For(level.Id) == GameChapter.Winter))
        {
            var session = new GameSession(level, SimulationSettings.Default);
            var presenter = new GameScenePresenter(level, visual);
            presenter.Apply(session.Snapshot, GameSkins.All[0]);
            var originals = presenter.CoinObjects.ToDictionary(pair => pair.Key, pair => pair.Value);
            int count = presenter.Scene.Objects.Count;
            Assert.HasCount(level.Coins.Count, originals);
            Assert.HasCount(4, originals.Values.Select(item => item.Material).Distinct());
            foreach (var coin in level.Coins)
            {
                var item = originals[coin.Id];
                mesh ??= item.Mesh;
                Assert.AreSame(mesh, item.Mesh);
                Assert.AreNotSame(GameMeshes.Coin, item.Mesh);
                Assert.AreEqual(coin.Center, item.Transform.Position);
                Assert.AreEqual(new Vector3(coin.Radius), item.Transform.Scale);
            }
            // Подойти к первому подарку настоящими командами: часть стартов находится в X=-1.
            for (int tick = 0; tick < 120 && session.Snapshot.CoinCount == 0; tick++)
            {
                Vector3 delta = level.Coins[0].Center - session.Snapshot.Ball.Center;
                var move = new Vector2(delta.X, delta.Z);
                if (move.LengthSquared() > 1) move = Vector2.Normalize(move);
                session.Step(new(move, false, false));
                presenter.Apply(session.Snapshot, GameSkins.All[0], session.LastPath);
            }
            Assert.IsGreaterThan(0, session.Snapshot.CoinCount, level.Id);
            foreach (string id in session.Snapshot.CollectedCoinIds)
                Assert.IsFalse(presenter.Scene.Objects.Contains(originals[id]));
            Assert.HasCount(count - session.Snapshot.CoinCount, presenter.Scene.Objects,
                "После сбора не должны оставаться отдельные ленты и банты.");

            var restart = new GameSession(level, SimulationSettings.Default);
            presenter.Apply(restart.Snapshot, GameSkins.All[0]);
            Assert.HasCount(count, presenter.Scene.Objects);
            foreach (var (id, item) in originals)
            {
                Assert.AreSame(item, presenter.CoinObjects[id]);
                Assert.IsTrue(presenter.Scene.Objects.Contains(item));
            }
        }
        foreach (var vertex in mesh!.Vertices)
            Assert.IsLessThanOrEqualTo(1.00001f, vertex.Position.Length(),
                "Вся упаковка должна помещаться в существующую сферу сбора.");
        var tropical = new GameScenePresenter(catalog.Levels[0], visual);
        Assert.AreNotSame(mesh, tropical.CoinObjects.Values.First().Mesh);
    }

    [TestMethod]
    public void HudUsesGiftsOnlyForWinterAndKeepsTheActualCollectionCount()
    {
        var host = new GameplayHost(GameLevels.CreateCatalog(), SimulationSettings.Default, new());
        foreach (string id in new[] { "winter-snowbound", "winter-glacier-ferries", "level1" })
        {
            host.Flow.Start(id);
            host.Advance(new(true), SimulationSettings.Default.FixedStepSeconds);
            host.RefreshView(1280, 720);
            string label = GameChapters.For(id) == GameChapter.Winter ? "Gifts" : "Coins";
            var snapshot = host.Flow.Snapshot!;
            int total = GameLevels.CreateCatalog().Get(id).Coins.Count;
            Assert.IsTrue(host.View.Texts.Any(text => text.Text == $"{label} {snapshot.CoinCount}/{total}"));
            Assert.IsFalse(host.View.Texts.Any(text => text.Text.StartsWith(label == "Gifts" ? "Coins " : "Gifts ")));
        }
    }

    [TestMethod]
    public void ProductionFerriesKeepTheirTravelAndCarryTheBallAtOneAndAHalfSpeed()
    {
        var catalog = GameLevels.CreateCatalog();
        foreach (var level in catalog.Levels)
        foreach (var ferry in level.Surfaces.Where(surface => surface.Motion is not null))
        {
            var motion = ferry.Motion!;
            float oldPeriod = level.Id == "winter-glacier-ferries" ? 20 : 16;
            var previous = new PlatformMotion(motion.Travel, oldPeriod);
            for (int sample = 0; sample <= 24; sample++)
            {
                double seconds = oldPeriod * sample / 24d;
                Assert.IsLessThan(0.00001f, Vector3.Distance(previous.Offset(seconds * 1.5), motion.Offset(seconds)));
            }

            var isolated = new LevelDefinition("ferry-test", ferry.Bounds.Center + Vector3.UnitY * (ferry.Bounds.HalfExtents.Y + 0.501f),
                -20, new(new(300, 20, 0), Vector3.One, Quaternion.Identity), [ferry], [], []);
            var session = new GameSession(isolated, SimulationSettings.Default);
            var presenter = new GameScenePresenter(isolated);
            int ticks = (int)Math.Ceiling(motion.Period / SimulationSettings.Default.FixedStepSeconds);
            for (int tick = 0; tick < ticks; tick++) session.Step(default);
            Assert.IsTrue(session.Snapshot.Ball.Grounded);
            Assert.AreEqual(SessionPhase.Running, session.Snapshot.Phase);
            var position = ferry.BoundsAt(session.Snapshot.SimulationSeconds).Center;
            Assert.AreEqual(position.X, session.Snapshot.Ball.Center.X, 0.02f);
            presenter.Apply(session.Snapshot, GameSkins.All[0]);
            Assert.AreEqual(position, presenter.SurfaceObjects[ferry.Id].Transform.Position);
        }
    }
}
