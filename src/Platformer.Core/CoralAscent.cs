using System.Numerics;
using static Platformer.Core.ChallengeGeometry;

namespace Platformer.Core;

/// <summary>Два подъёма, прыжок вверх, узкие террасы и необязательная боковая ветка.</summary>
public static class CoralAscent
{
    public static LevelDefinition Create() => new("coral-ascent", new(-1, 0.501f, 0), -6,
        Finish(new(8, 2.8f, 20.6f), true),
        [
            Flat("start", 0, 0, 0, 4, 4),
            Between("first-climb", new(2, 0, 0), new(7, 1.2f, 0), 2),
            Flat("first-terrace", 9, 1.2f, 0, 4, 4),
            Between("thin-neck", new(9, 1.2f, 2), new(9, 1.2f, 6), 1.25f),
            Flat("takeoff-up", 9, 1.2f, 7, 3.2f, 2),
            Flat("landing-up", 9, 1.6f, 11, 2.8f, 2.8f),
            Between("second-climb", new(10.4f, 1.6f, 11), new(15.4f, 2.8f, 11), 1.5f),
            Flat("high-terrace", 17.4f, 2.8f, 11, 4, 4),
            Between("bonus-neck", new(17.4f, 2.8f, 9), new(17.4f, 2.8f, 6.4f), 1.1f),
            Flat("bonus-balcony", 17.4f, 2.8f, 5.4f, 2.4f, 2),
            Flat("takeoff-high", 17.4f, 2.8f, 15, 2.4f, 4),
            Flat("landing-high", 17.4f, 2.8f, 20.6f, 3.4f, 4),
            Between("last-balance", new(15.7f, 2.8f, 20.6f), new(10, 2.8f, 20.6f), 1.2f),
            Flat("finish-platform", 8, 2.8f, 20.6f, 4, 4),
        ],
        Coins(new(-0.5f, 0, 0), new(4.5f, 0.6f, 0), new(9, 1.2f, 0), new(9, 1.2f, 4),
            new(9, 1.2f, 7), new(9, 1.6f, 11), new(12.9f, 2.2f, 11), new(17.4f, 2.8f, 11),
            new(17.4f, 2.8f, 5.4f), new(16.9f, 2.8f, 5.4f), new(17.4f, 2.8f, 20.6f), new(9, 2.8f, 20.6f)), []);
}
