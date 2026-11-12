using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Platformer.App;
using Platformer.Core;

namespace Platformer.Tests;

/// <summary>Маршруты через настоящий ввод host, без телепортов или подмены Step.</summary>
internal static class ChallengeRoutes
{
    internal static readonly string[] Ids = ["coral-ascent", "windmill-pass", "lava-causeway", "crumble-canyon", "temple-gauntlet"];
    internal static readonly string[] ExpertIds = ["pendulum-gorge", "piston-foundry", "clockwork-summit"];

    internal static void Run(ProductionRouteTests.Driver d, string id)
    {
        void Go(float x, float z) => d.Go(x, z);
        void Hop(float edge, Vector2 direction, float x, float z)
        {
            var ball = d.Host.Flow.Snapshot!.Ball;
            Vector2 position = new(ball.Center.X, ball.Center.Z);
            Vector2 approach = position + direction * (edge - Vector2.Dot(position, direction) - 0.8f);
            Go(approach.X, approach.Y);
            for (int tick = 0; tick < 180; tick++)
            {
                ball = d.Host.Flow.Snapshot!.Ball;
                if (Vector2.Dot(new(ball.Center.X, ball.Center.Z), direction) >= edge - 0.3f) break;
                d.Move(direction);
            }
            Assert.IsTrue(d.Host.Flow.Snapshot!.Ball.Grounded, $"{id} takeoff {edge}: {d.Host.Flow.Snapshot.Ball.Center}");
            d.Move(direction, jump: true);
            Go(x, z);
            for (int tick = 0; tick < 90 && !d.Host.Flow.Snapshot!.Ball.Grounded; tick++)
            {
                ball = d.Host.Flow.Snapshot.Ball;
                d.Move(new Vector2(x - ball.Center.X, z - ball.Center.Z) * 5 - new Vector2(ball.Velocity.X, ball.Velocity.Z));
            }
            Assert.IsTrue(d.Host.Flow.Snapshot!.Ball.Grounded, $"{id} landing {x},{z}");
        }

        void Cross(string hazardId, float x, float z)
        {
            Go(x - 2, z);
            var definition = GameLevels.CreateCatalog().Get(id).Hazards.Single(h => h.Id == hazardId);
            bool window = false;
            for (int tick = 0; tick < 1200; tick++)
            {
                double time = d.Host.Flow.Snapshot!.SimulationSeconds;
                // Начать разгон так, чтобы середина пересечения пришлась на уход
                // камня/толкателя к краю. Ожидание — обычный ввод, время не меняется вручную.
                if (MathF.Abs(HazardMotion.Evaluate(definition, time + 0.6).Bounds.Center.Z - z) > 2.8f)
                { window = true; break; }
                var ball = d.Host.Flow.Snapshot.Ball;
                d.Move(new Vector2(x - 2 - ball.Center.X, z - ball.Center.Z) * 5
                    - new Vector2(ball.Velocity.X, ball.Velocity.Z));
            }
            Assert.IsTrue(window, $"{id}/{hazardId}: no crossing window");
            Go(x + 2, z);
        }

        switch (id)
        {
            case "coral-ascent":
                Go(-0.5f, 0); Go(9, 0); Go(9, 4); Go(9, 7);
                Hop(8, Vector2.UnitY, 9, 11); Go(17.4f, 11);
                Go(17.4f, 5.4f); Go(16.9f, 5.4f); Go(17.4f, 5.4f); Go(17.4f, 11); Go(17.4f, 15);
                Hop(17, Vector2.UnitY, 17.4f, 20.6f); Go(8, 20.6f);
                break;
            case "windmill-pass":
                Go(0, 0); Go(7.6f, 0); Go(8, 3.4f); Go(13.8f, 3.4f); Go(16, 2.6f); Go(20.5f, 2.6f);
                Go(20.5f, 8.5f); Go(23.35f, 9); Go(23.35f, 14.5f); Go(20.5f, 14.6f); Go(20.5f, 20.1f);
                Hop(21.1f, Vector2.UnitY, 20.5f, 24.1f); Go(20.5f, 27.5f);
                break;
            case "lava-causeway":
                Go(0.5f, 0); d.Hurdle(5); d.Hurdle(9); d.Hurdle(13); Go(16, 0);
                Go(16, -5.2f); Go(16.6f, -5.2f); Go(16, -5.2f); Go(16, 0);
                Go(16, 4.5f); Go(16, 9); d.Hurdle(20, 9); d.Hurdle(24, 9); Go(28, 9);
                break;
            case "crumble-canyon":
                Go(0.2f, 0);
                Hop(2, Vector2.UnitX, 4.8f, 0); Hop(6.2f, Vector2.UnitX, 9, 0);
                Hop(10.4f, Vector2.UnitX, 13.2f, 0); Hop(14.6f, Vector2.UnitX, 17.6f, 0);
                Go(23.6f, 0); Go(24.5f, 0); Go(17.6f, 0); Go(17.6f, 6.5f);
                Hop(8, Vector2.UnitY, 17.6f, 10.8f); Hop(12.2f, Vector2.UnitY, 17.6f, 15);
                Hop(16.4f, Vector2.UnitY, 17.6f, 19.4f); Go(10.2f, 19.4f);
                break;
            case "temple-gauntlet":
                Go(0.5f, 0); Go(9, 0); Go(9, -6.5f); Go(9.7f, -6.5f); Go(9, -6.5f); Go(9, 0);
                Go(11, 0); Go(15, 3); Go(15, 4.5f); Go(11, 8.5f); Go(11, 10);
                Go(17.2f, 10); Go(17.2f, 13.3f); Go(23.5f, 13.3f); Go(28.5f, 12.8f);
                d.Hurdle(30.5f, 12.8f); Go(32, 14.5f); Go(32, 19);
                Hop(20, Vector2.UnitY, 32, 23); Hop(24.4f, Vector2.UnitY, 32, 27.4f); Go(32, 28);
                break;
            case "pendulum-gorge":
                Go(0, 0); Cross("stone-one", 6, 0); Cross("stone-two", 10, 0); Go(14, 0);
                Go(14, 5); Go(14, 10); Go(17, 10); Cross("stone-three", 21, 10); Go(28, 10);
                Hop(30, Vector2.UnitX, 33.8f, 10); Go(35, 10);
                break;
            case "piston-foundry":
                Go(0, 0); Cross("piston-one", 6, 0); Cross("piston-two", 11, 0); Cross("piston-three", 16, 0);
                Go(20, 0); Go(20, 4.5f); Go(20, 9);
                Hop(22, Vector2.UnitX, 25.2f, 9); Hop(26.6f, Vector2.UnitX, 29.4f, 9);
                Hop(30.8f, Vector2.UnitX, 33, 9); Go(34, 9);
                break;
            case "clockwork-summit":
                Go(0, 0); Go(9, 0); Cross("summit-stone", 15.5f, 0); Go(22, 0); Go(22, 2);
                Go(24.5f, 4.5f); Go(27, 7); Go(27, 8.5f); Cross("summit-piston", 32, 8.5f);
                d.Hurdle(38.5f, 8.5f); Go(40.5f, 8.5f);
                Hop(41.5f, Vector2.UnitX, 44.5f, 8.5f); Hop(45.9f, Vector2.UnitX, 49.1f, 8.5f);
                Go(49.1f, 10); Go(45.1f, 12); Go(40.2f, 12);
                break;
            default: throw new AssertFailedException(id);
        }
    }
}
