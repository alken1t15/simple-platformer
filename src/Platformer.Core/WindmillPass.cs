using System.Numerics;
using static Platformer.Core.ChallengeGeometry;

namespace Platformer.Core;

/// <summary>Две встречные вращающиеся балки, обходы по краю и переход на верхнюю арену.</summary>
public static class WindmillPass
{
    public static LevelDefinition Create() => new("windmill-pass", new(-1, 0.501f, 0), -6,
        Finish(new(20.5f, 1, 27.5f), false),
        [
            Flat("start", 0, 0, 0, 4, 4),
            Between("entry-ropewalk", new(2, 0, 0), new(7, 0, 0), 1.5f),
            Flat("first-arena", 11, 0, 0, 8, 8),
            Between("side-exit", new(15, 0, 2.6f), new(19, 0, 2.6f), 1.35f),
            Flat("turn", 20.5f, 0, 2.6f, 3, 3),
            Between("arena-climb", new(20.5f, 0, 4.1f), new(20.5f, 1, 8.1f), 1.5f),
            Flat("second-arena", 20.5f, 1, 11.6f, 7, 7),
            Between("exit-ropewalk", new(20.5f, 1, 15.1f), new(20.5f, 1, 19.1f), 1.1f),
            Flat("takeoff", 20.5f, 1, 20.1f, 3.4f, 2),
            Flat("landing", 20.5f, 1, 24.1f, 4, 2.8f),
            Flat("finish-platform", 20.5f, 1, 27.5f, 4, 4),
        ],
        Coins(new(0, 0, 0), new(4.5f, 0, 0), new(7.6f, 0, 0), new(8, 0, 3.4f), new(13.8f, 0, 3.4f),
            new(20.5f, 0, 2.6f), new(20.5f, 0.5f, 6.1f), new(23.35f, 1, 9), new(23.35f, 1, 11.6f),
            new(20.5f, 1, 14.6f), new(20.5f, 1, 20.1f), new(20.5f, 1, 24.1f)),
        [Bumper("lower-windmill", new(11, 0, 0), 2.6f, 1.05f),
         Bumper("upper-windmill", new(20.5f, 1, 11.6f), 2.3f, -1.3f, 0.5f)]);
}
