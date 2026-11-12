using System.Numerics;
using static Platformer.Core.ChallengeGeometry;
using static Platformer.Core.ExpertGeometry;

namespace Platformer.Core;

/// <summary>Узкие мосты под тремя качающимися камнями; между проходами есть места ожидания.</summary>
public static class PendulumGorge
{
    public static LevelDefinition Create() => new("pendulum-gorge", new(-1, 0.501f, 0), -6,
        Finish(new(35, 0, 10), true),
        [Flat("start", 0, 0, 0, 4, 4), Between("first-bridge", new(2, 0, 0), new(12, 0, 0), 1.7f),
         Flat("rest-one", 14, 0, 0, 4, 4), Between("turn-neck", new(14, 0, 2), new(14, 0, 8), 1.1f),
         Flat("rest-two", 14, 0, 10, 4, 4), Between("last-bridge", new(16, 0, 10), new(26, 0, 10), 1.5f),
         Flat("takeoff", 28, 0, 10, 4, 4), Flat("finish-platform", 33.8f, 0, 10, 4, 3),
         ..Gantry("arch-one", 6, 0, 0), ..Gantry("arch-two", 10, 0, 0), ..Gantry("arch-three", 21, 0, 10)],
        Coins(new(0, 0, 0), new(3, 0, 0), new(6, 0, 0), new(10, 0, 0), new(14, 0, 0),
            new(14, 0, 5), new(14, 0, 10), new(17, 0, 10), new(21, 0, 10), new(25, 0, 10),
            new(28, 0, 10), new(34, 0, 10)),
        [Pendulum("stone-one", 6, 0, 0), Pendulum("stone-two", 10, 0, 0, 1.4f), Pendulum("stone-three", 21, 0, 10, 2.4f)]);
}
