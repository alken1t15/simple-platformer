using System.Numerics;
using static Platformer.Core.ChallengeGeometry;

namespace Platformer.Core;

/// <summary>Смертельные вулканические барьеры с местами для разгона и узкой боковой веткой.</summary>
public static class LavaCauseway
{
    public static LevelDefinition Create() => new("lava-causeway", new(-1, 0.501f, 0), -6,
        Finish(new(28, 0, 9), true),
        [
            Flat("start", 0, 0, 0, 4, 4), Flat("first-runway", 8, 0, 0, 12, 2.8f),
            Flat("corner", 16, 0, 0, 4, 4),
            Between("bonus-neck", new(16, 0, -2), new(16, 0, -4), 0.95f),
            Flat("bonus-balcony", 16, 0, -5.2f, 2.4f, 2.4f),
            Between("canyon-bridge", new(16, 0, 2), new(16, 0, 7), 1.1f),
            Flat("second-turn", 16, 0, 9, 4, 4), Flat("second-runway", 22, 0, 9, 8, 2.8f),
            Flat("finish-platform", 28, 0, 9, 4, 4),
        ],
        Coins(new(0.5f, 0, 0), new(3, 0, 0), new(5, 0.55f, 0), new(7, 0, 0), new(9, 0.55f, 0),
            new(11, 0, 0), new(13, 0.55f, 0), new(16, 0, -5.2f), new(16.6f, 0, -5.2f),
            new(16, 0, 4.5f), new(20, 0.55f, 9), new(24, 0.55f, 9)),
        [Hurdle("lava-one", new(5, 0, 0), 3), Hurdle("lava-two", new(9, 0, 0), 3),
         Hurdle("lava-three", new(13, 0, 0), 3), Hurdle("lava-four", new(20, 0, 9), 3),
         Hurdle("lava-five", new(24, 0, 9), 3)]);
}
