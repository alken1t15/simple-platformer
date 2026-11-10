using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Platformer.App;
using Platformer.Core;

namespace Platformer.Tests;

[TestClass]
public sealed class LevelRefinementTests
{
    [TestMethod]
    public void MazePurpleRailStopsGroundedBallInsteadOfLettingItFallThrough()
    {
        var level = WithSpawn(TurningMaze.Create(), new(5, 0.501f, 0));
        var simulator = new BallSimulator(level, SimulationSettings.Default);
        var ball = simulator.CreateInitialState();
        for (int i = 0; i < 180; i++)
            ball = simulator.Step(ball, new(Vector2.UnitY, false, false)).Ball;
        Assert.IsLessThanOrEqualTo(0.752f, ball.Center.Z);
        Assert.IsTrue(ball.Grounded);
        Assert.AreEqual(0f, ball.Velocity.Z, 0.001f);
    }

    [TestMethod]
    public void MazeRotatingBeamPushesBallAwayAndAttemptContinues()
    {
        var session = new GameSession(WithSpawn(TurningMaze.Create(), new(20, 0.501f, 9.2f)), SimulationSettings.Default);
        bool pushed = false;
        for (int i = 0; i < 90; i++)
        {
            session.Step(new(-Vector2.UnitY, false, false));
            Assert.AreEqual(SessionPhase.Running, session.Snapshot.Phase);
            if (session.Snapshot.Ball.Velocity.Y > 2f && session.Snapshot.Ball.Velocity.Z > 2f)
            {
                pushed = true;
                break;
            }
        }
        Assert.IsTrue(pushed, "Балка должна оттолкнуть шар вверх и от себя");
        float contactZ = session.Snapshot.Ball.Center.Z;
        for (int i = 0; i < 12; i++) session.Step(new(Vector2.Zero, false, false));
        Assert.AreEqual(SessionPhase.Running, session.Snapshot.Phase);
        Assert.IsGreaterThan(contactZ + 0.15f, session.Snapshot.Ball.Center.Z,
            "Шар должен выйти из контакта, а не застыть в повторных ударах");
    }

    [TestMethod]
    public void RequestedGapsAreTwoUnitsWide()
    {
        Check(TrainingTrack.Create(), "jump-takeoff", "jump-landing", true);
        Check(TurningMaze.Create(), "takeoff", "landing", false);
        Check(IslandCircuit.Create(), "takeoff", "landing", false);

        static void Check(LevelDefinition level, string from, string to, bool alongZ)
        {
            var a = level.Surfaces.Single(surface => surface.Id == from).Bounds;
            var b = level.Surfaces.Single(surface => surface.Id == to).Bounds;
            float gap = alongZ ? b.Center.Z - b.HalfExtents.Z - a.Center.Z - a.HalfExtents.Z
                : b.Center.X - b.HalfExtents.X - a.Center.X - a.HalfExtents.X;
            Assert.AreEqual(2f, gap, 0.0001f, level.Id);
        }
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(4)]
    public void WiderGapNeedsJumpAndCanBeCrossedWithoutBoost(int number)
    {
        var source = number == 1 ? TrainingTrack.Create() : number == 2 ? TurningMaze.Create() : IslandCircuit.Create();
        bool alongZ = number == 1;
        var takeoff = source.Surfaces.Single(s => s.Id == (alongZ ? "jump-takeoff" : "takeoff")).Bounds;
        var landing = source.Surfaces.Single(s => s.Id == (alongZ ? "jump-landing" : "landing")).Bounds;
        Vector3 direction = alongZ ? Vector3.UnitZ : Vector3.UnitX;
        Vector2 move = alongZ ? Vector2.UnitY : Vector2.UnitX;
        float edge = alongZ ? takeoff.Center.Z + takeoff.HalfExtents.Z : takeoff.Center.X + takeoff.HalfExtents.X;
        float near = alongZ ? landing.Center.Z - landing.HalfExtents.Z : landing.Center.X - landing.HalfExtents.X;
        Vector3 spawn = takeoff.Center with { Y = takeoff.Center.Y + takeoff.HalfExtents.Y + 0.501f };
        if (alongZ) spawn.Z = edge - 0.35f; else spawn.X = edge - 0.35f;
        var simulator = new BallSimulator(WithSpawn(source, spawn), SimulationSettings.Default);
        var initial = simulator.CreateInitialState() with { Velocity = direction * SimulationSettings.Default.MoveSpeed };
        BallState Cross(bool jump)
        {
            var ball = initial;
            for (int tick = 0; tick < 100; tick++)
            {
                ball = simulator.Step(ball, new(move, jump && tick == 0, false)).Ball;
                if (ball.Grounded && Vector3.Dot(ball.Center, direction) > near + 0.1f) break;
            }
            return ball;
        }
        var crossed = Cross(true);
        Assert.IsTrue(crossed.Grounded, source.Id);
        Assert.IsGreaterThan(near, Vector3.Dot(crossed.Center, direction), source.Id);
        Assert.AreEqual(0f, crossed.BoostRemainingSeconds);
        Assert.IsLessThan(spawn.Y - 1f, Cross(false).Center.Y, "Без прыжка шар должен упасть в широкий разрыв");
    }

    [TestMethod]
    public void IslandBeamAlsoKnocksBackAndDefaultHazardsRemainFatal()
    {
        var level = IslandCircuit.Create();
        var beam = level.Hazards.Single();
        var pose = HazardMotion.Evaluate(beam, 0).Bounds;
        Vector3 normal = Vector3.Transform(Vector3.UnitZ, pose.Orientation);
        var spawn = pose.Center + normal * 1.1f;
        spawn.Y = pose.Center.Y - 0.55f + 0.501f;
        var session = new GameSession(WithSpawn(level, spawn), SimulationSettings.Default);
        bool bounced = false;
        for (int tick = 0; tick < 90 && !bounced; tick++)
        {
            session.Step(new(-new Vector2(normal.X, normal.Z), false, false));
            Assert.AreEqual(SessionPhase.Running, session.Snapshot.Phase);
            bounced = session.Snapshot.Ball.Velocity.Y > 2f;
        }
        Assert.IsTrue(bounced);
        Assert.IsTrue(JumpCircuit.Create().Hazards.All(h => h.Response == HazardResponse.Fail));
        Assert.IsTrue(SkyStorm.Create().Hazards.All(h => h.Response == HazardResponse.Fail));
    }

    [TestMethod]
    public void KnockbackCancelsCrossingPathWithoutCollectingItsCoinOrFinishAndRestartClearsIt()
    {
        var source = Arena(new(0, 0.501f, -1.2f));
        var end = new Vector3(0, 0.501f, 1.2f);
        var level = new LevelDefinition(source.Id, source.Spawn, source.KillY,
            new(end, new(0.05f), Quaternion.Identity), source.Surfaces,
            [new("blocked-coin", end + Vector3.UnitY * 0.3f, 0.2f)], source.Hazards);
        var session = new GameSession(level, SimulationSettings.Default, (ball, _) =>
            new(ball with { Center = end, Velocity = Vector3.UnitZ * 8.1f }, [new(ball.Center, end, 0, 1)]));
        session.Step(default);
        Assert.AreEqual(SessionPhase.Running, session.Snapshot.Phase);
        Assert.AreEqual(source.Spawn, session.Snapshot.Ball.Center);
        Assert.AreEqual(0, session.Snapshot.CoinCount);
        Assert.IsLessThan(-2f, session.Snapshot.Ball.Velocity.Z);
        Assert.IsTrue(session.LastPath.All(s => s.Start == source.Spawn && s.End == source.Spawn));
        session.Restart();
        Assert.AreEqual(source.Spawn, session.Snapshot.Ball.Center);
        Assert.AreEqual(Vector3.Zero, session.Snapshot.Ball.Velocity);
        Assert.IsEmpty(session.LastPath);
        Assert.IsEmpty(session.DrainEvents());
    }

    [TestMethod]
    public void KnockbackNextToWallNeverPushesThroughStaticGeometryOrStacksSpeed()
    {
        var source = Arena(new(0, 0.501f, -1.1f));
        var wall = new SurfaceDefinition("back-wall", new(new(0, 1.5f, -1.8f), new(3, 1.5f, 0.1f), Quaternion.Identity));
        var level = new LevelDefinition(source.Id, source.Spawn, source.KillY, source.Finish,
            source.Surfaces.Append(wall).ToArray(), [], source.Hazards);
        var session = new GameSession(level, SimulationSettings.Default);
        bool bounced = false;
        for (int tick = 0; tick < 240; tick++)
        {
            session.Step(new(Vector2.UnitY, false, false));
            var ball = session.Snapshot.Ball;
            Assert.AreEqual(SessionPhase.Running, session.Snapshot.Phase);
            bounced |= ball.Velocity.Y > 2;
            Assert.IsGreaterThanOrEqualTo(-SimulationSettings.Default.PenetrationSlop,
                SphereGeometry.Contact(wall.Bounds, ball.Center, ball.Radius).Separation);
            Assert.IsLessThanOrEqualTo(7.6f, new Vector2(ball.Velocity.X, ball.Velocity.Z).Length());
        }
        Assert.IsTrue(bounced);
    }

    [TestMethod]
    public void FatalHazardKeepsPriorityOverKnockbackInSameTick()
    {
        var source = Arena(new(0, 0.501f, -1.1f));
        var beam = source.Hazards.Single();
        var fatal = new HazardDefinition("fatal", beam.Pivot, beam.Axis, beam.CenterOffset,
            beam.HalfExtents, beam.InitialOrientation, 0, 0);
        var level = new LevelDefinition(source.Id, source.Spawn, source.KillY, source.Finish,
            source.Surfaces, [], source.Hazards.Append(fatal).ToArray());
        var session = new GameSession(level, SimulationSettings.Default);
        for (int tick = 0; tick < 90 && session.Snapshot.Phase == SessionPhase.Running; tick++)
            session.Step(new(Vector2.UnitY, false, false));
        Assert.AreEqual(SessionPhase.Failed, session.Snapshot.Phase);
        Assert.AreEqual(1, session.DrainEvents().Count(e => e.Kind == GameEventKind.Failed));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void PurpleBarrierRenderingUsesExactPhysicalBounds(bool visual)
    {
        var level = TurningMaze.Create();
        var presenter = new GameScenePresenter(level, visual);
        var rails = level.Surfaces.Where(s => s.IsBarrier).ToArray();
        Assert.HasCount(6, rails);
        foreach (var rail in rails)
        {
            var item = presenter.SurfaceObjects[rail.Id];
            Assert.AreEqual(rail.Bounds.Center, item.Transform.Position);
            Assert.AreEqual(rail.Bounds.HalfExtents * 2, item.Transform.Scale);
            Assert.AreEqual(rail.Bounds.Orientation, item.Transform.Rotation);
        }
    }

    private static LevelDefinition Arena(Vector3 spawn) => new("arena-test", spawn, -5,
        new(new(8, 1, 8), new(0.1f), Quaternion.Identity),
        [new("floor", new(new(0, -0.2f, 0), new(10, 0.2f, 10), Quaternion.Identity))], [],
        [new("beam", new(0, 0.55f, 0), Vector3.UnitY, Vector3.Zero, new(2, 0.22f, 0.22f),
            Quaternion.Identity, 0, 0, HazardResponse.Knockback)]);

    private static LevelDefinition WithSpawn(LevelDefinition level, Vector3 spawn) =>
        new(level.Id, spawn, level.KillY, level.Finish, level.Surfaces, level.Coins, level.Hazards);
}
