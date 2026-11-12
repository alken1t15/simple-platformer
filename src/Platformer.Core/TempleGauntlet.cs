using System.Numerics;
using static Platformer.Core.ChallengeGeometry;

namespace Platformer.Core;

/// <summary>Подъём, диагональный слалом, вращение, смертельный барьер и финальная пара прыжков.</summary>
public static class TempleGauntlet
{
    public static LevelDefinition Create() => new("temple-gauntlet", new(-1, 0.501f, 0), -6,
        Finish(new(32, 3, 27.8f), false),
        [
            Flat("start", 0, 0, 0, 4, 4),
            Between("temple-climb", new(2, 0, 0), new(7, 1.5f, 0), 1.6f),
            Flat("first-terrace", 9, 1.5f, 0, 4, 4),
            Between("bonus-neck", new(9, 1.5f, -2), new(9, 1.5f, -5), 0.95f),
            Flat("bonus-balcony", 9, 1.5f, -6.5f, 3, 3),
            Between("slalom-one", new(11, 1.5f, 0), new(15, 1.5f, 3), 1.05f),
            Flat("slalom-turn", 15, 1.5f, 3, 3, 3),
            Between("slalom-two", new(15, 1.5f, 4.5f), new(11, 1.5f, 8.5f), 1.05f),
            Flat("slalom-landing", 11, 1.5f, 10, 3, 3),
            Between("arena-entry", new(12.5f, 1.5f, 10), new(16.5f, 1.5f, 10), 1.25f),
            Flat("temple-arena", 20.5f, 1.5f, 10, 8, 8),
            Between("exit-climb", new(24.5f, 1.5f, 12.8f), new(28.5f, 2.5f, 12.8f), 1.3f),
            Flat("hurdle-terrace", 31, 2.5f, 12.8f, 5, 4),
            Between("final-neck", new(32, 2.5f, 14.8f), new(32, 2.5f, 18), 1.2f),
            Flat("takeoff", 32, 2.5f, 19, 3, 2),
            Flat("crumb-landing", 32, 3, 23, 3.4f, 2.8f, 1.75f),
            Flat("finish-platform", 32, 3, 27.4f, 4, 3.2f),
        ],
        Coins(new(0.5f, 0, 0), new(4.5f, 0.75f, 0), new(9, 1.5f, 0), new(9, 1.5f, -6.5f),
            new(9.7f, 1.5f, -6.5f), new(15, 1.5f, 3), new(11, 1.5f, 10), new(17.2f, 1.5f, 13.3f),
            new(23.5f, 1.5f, 13.3f), new(30.5f, 3.05f, 12.8f), new(32, 3, 23), new(32, 3, 27.1f)),
        [Bumper("temple-sweeper", new(20.5f, 1.5f, 10), 2.5f, -1.25f, 0.6f),
         Hurdle("temple-hurdle", new(30.5f, 2.5f, 12.8f), 4.2f)]);
}
