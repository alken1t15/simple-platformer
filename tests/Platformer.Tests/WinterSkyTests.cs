using System.Numerics;
using Engine3D.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Platformer.App;
using Platformer.Core;

namespace Platformer.Tests;

[TestClass]
public sealed class WinterSkyTests
{
    [TestMethod]
    public void NewWinterScenesStartTheSleighAtDifferentSkyPositions()
    {
        var level = GameLevels.CreateCatalog().Get("winter-snowbound");
        var starts = Enumerable.Range(0, 6).Select(_ => new GameScenePresenter(level, true).Scene.Objects
            .Single(o => o.Transform.Scale == new Vector3(2.8f, 0.5f, 1.8f)).Transform.Position).ToArray();
        Assert.HasCount(6, starts.Distinct().ToArray());
    }

    [TestMethod]
    public void ReindeerKneesStayConnectedWhileTheirLegsBend()
    {
        var level = GameLevels.CreateCatalog().Get("winter-snowbound");
        var presenter = new GameScenePresenter(level, true);
        var upper = presenter.Scene.Objects.Where(o => o.Transform.Scale == new Vector3(0.14f, 0.42f, 0.14f)).ToArray();
        var lower = presenter.Scene.Objects.Where(o => o.Transform.Scale == new Vector3(0.11f, 0.42f, 0.11f)).ToArray();
        var hoof = presenter.Scene.Objects.Where(o => o.Transform.Scale == new Vector3(0.24f, 0.16f, 0.2f)).ToArray();
        Assert.HasCount(16, upper); Assert.HasCount(16, lower); Assert.HasCount(16, hoof);
        var angles = new List<float[]>();
        foreach (double seconds in new[] { 0d, 0.2, 0.4, 0.6, 0.8, 1d })
        {
            Apply(presenter, level, seconds);
            var bend = new float[16];
            for (int i = 0; i < upper.Length; i++)
            {
                Vector3 u = Vector3.Transform(Vector3.UnitY, upper[i].Transform.Rotation);
                Vector3 l = Vector3.Transform(Vector3.UnitY, lower[i].Transform.Rotation);
                Assert.IsLessThan(0.0001f, Vector3.Distance(upper[i].Transform.Position + u * 0.21f,
                    lower[i].Transform.Position - l * 0.21f), "detached knee");
                Assert.IsLessThan(0.1f, Vector3.Distance(lower[i].Transform.Position + l * 0.21f,
                    hoof[i].Transform.Position), "detached hoof");
                bend[i] = Vector3.Dot(u, l);
            }
            angles.Add(bend);
        }
        for (int i = 0; i < 16; i++)
            Assert.IsGreaterThan(0.1f, angles.Max(a => a[i]) - angles.Min(a => a[i]), "rigid leg");
    }

    [TestMethod]
    public void WinterCoinsStayClearOfTheFinishGate()
    {
        foreach (var level in GameLevels.CreateCatalog().Levels.Where(l => GameChapters.For(l.Id) == GameChapter.Winter))
        foreach (var coin in level.Coins)
            Assert.IsGreaterThan(coin.Radius + 0.55f,
                Vector3.Distance(coin.Center, level.Finish.ClosestPoint(coin.Center)), level.Id + " " + coin.Id);
    }

    [TestMethod]
    public void WinterParadeMovesAboveTheTrackAndReusesItsSceneObjects()
    {
        foreach (var level in GameLevels.CreateCatalog().Levels.Where(l => GameChapters.For(l.Id) == GameChapter.Winter))
        {
            var presenter = new GameScenePresenter(level, true);
            Apply(presenter, level, 0);
            var all = presenter.Scene.Objects.ToArray();
            var start = all.Select(o => o.Transform.Position).ToArray();
            Apply(presenter, level, 1);
            var flying = all.Where((o, i) => start[i].Y > 15 && o.Transform.Position != start[i]).ToArray();
            Assert.IsGreaterThan(150, flying.Length, level.Id + " missing sleigh team or trail");
            Assert.IsTrue(flying.Any(o => o.Material.BaseColor == new ColorRgba(183 / 255f, 34 / 255f, 58 / 255f)));
            foreach (double seconds in new[] { 0d, 0.6, 1.8, 12, 24, 47.9999, 48, 1e9 })
            {
                Apply(presenter, level, seconds);
                CollectionAssert.AreEqual(all, presenter.Scene.Objects.ToArray());
                foreach (var item in flying)
                {
                    item.Transform.Validate();
                    item.Material.Validate();
                    Assert.IsGreaterThan(15f, item.Transform.Position.Y, level.Id + " decoration too low");
                }
            }
            Apply(presenter, level, 0);
            var atZero = flying.Select(o => o.Transform.Position).ToArray();
            Apply(presenter, level, 144); // 48s flight, 1.2s gait and 1.8s particles repeat together.
            foreach (var (item, i) in flying.Select((o, i) => (o, i)))
                Assert.IsLessThan(0.0001f, Vector3.Distance(atZero[i], item.Transform.Position));
        }
    }

    [TestMethod]
    public void PauseFreezesTheSleighAndRestartRestoresItsInitialPose()
    {
        var host = new GameplayHost(GameLevels.CreateCatalog(), SimulationSettings.Default, new(), visual: true);
        host.Flow.Start("winter-snowbound"); host.Advance(new(true), 0);
        var parts = host.Flow.Presenter!.Scene.Objects.Where(o => o.Transform.Position.Y > 16 && o.Transform.Position.Y < 22).ToArray();
        var initial = parts.Select(o => o.Transform.GetModelMatrix()).ToArray();
        for (int i = 0; i < 120; i++) host.Advance(new(true), SimulationSettings.Default.FixedStepSeconds);
        var moving = parts.Select(o => o.Transform.GetModelMatrix()).ToArray();
        Assert.IsTrue(moving.Where((value, i) => value != initial[i]).Any());
        host.Flow.Pause();
        for (int i = 0; i < 120; i++) host.Advance(new(true), SimulationSettings.Default.FixedStepSeconds);
        CollectionAssert.AreEqual(moving, parts.Select(o => o.Transform.GetModelMatrix()).ToArray());
        host.Flow.Restart();
        CollectionAssert.AreEqual(initial, parts.Select(o => o.Transform.GetModelMatrix()).ToArray());
    }

    [TestMethod]
    public void TropicalSkyHasNoSantaAndWinterHasNoOpaqueAuroraBars()
    {
        var catalog = GameLevels.CreateCatalog();
        var tropical = new GameScenePresenter(catalog.Get("level1"), true);
        var winter = new GameScenePresenter(catalog.Get("winter-snowbound"), true);
        Assert.IsFalse(tropical.Scene.Objects.Any(o => o.Transform.Position.Y > 15
            && o.Material.BaseColor == new ColorRgba(183 / 255f, 34 / 255f, 58 / 255f)));
        Assert.IsFalse(winter.Scene.Objects.Any(o => o.Transform.Scale == new Vector3(7, 0.6f, 0.3f)));
        Assert.IsFalse(winter.Scene.Objects.Any(o => o.Transform.Scale == new Vector3(3.4f)));
    }

    private static void Apply(GameScenePresenter presenter, LevelDefinition level, double seconds)
    {
        var snapshot = new GameSession(level, SimulationSettings.Default).Snapshot;
        presenter.Apply(new SessionSnapshot(Guid.NewGuid(), level.Id, 0, seconds, SessionPhase.Running,
            snapshot.Ball, [], level.Hazards.Select(h => HazardMotion.Evaluate(h, seconds)).ToArray(), snapshot.Platforms), GameSkins.All[0]);
    }
}
