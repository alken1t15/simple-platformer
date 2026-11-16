using System.Numerics;
using static Platformer.Core.ChallengeGeometry;

namespace Platformer.Core;

/// <summary>Авторские маршруты второй главы. Снег/лёд оформляет App, физика общая.</summary>
public static class WinterLevels
{
    private static HazardDefinition Snowball(string id, float x, float top, float z, float phase = 0) =>
        new(id, new(x, top + 0.7f, z), Vector3.UnitZ, Vector3.Zero, new(0.65f), Quaternion.Identity,
            1.15f, phase, HazardResponse.Knockback, HazardMotionKind.Shuttle, 3.8f);

    private static HazardDefinition IcePress(string id, float x, float top, float z, float phase = 0) =>
        new(id, new(x, top + 2.5f, z), Vector3.UnitY, Vector3.Zero, new(0.6f, 0.65f, 1.8f), Quaternion.Identity,
            1.35f, phase, HazardResponse.Fail, HazardMotionKind.Shuttle, 1.8f);

    public static LevelDefinition SnowboundPass() => new("winter-snowbound", new(-1, 0.501f, 0), -7,
        Finish(new(29, 1.2f, 8), true),
        [Flat("start", 0, 0, 0, 4, 4), Between("snow-climb", new(2, 0, 0), new(8, 1.2f, 0), 2),
         Flat("pine-rest", 10, 1.2f, 0, 4, 4), Between("snowball-bridge", new(12, 1.2f, 0), new(20, 1.2f, 0), 2),
         Flat("turn", 22, 1.2f, 0, 4, 4), Between("balcony-neck", new(22, 1.2f, 2), new(22, 1.2f, 6), 1.35f),
         Flat("jump-island", 22, 1.2f, 8, 4, 4), Flat("finish-platform", 28, 1.2f, 8, 4.4f, 3.6f)],
        Coins(new(0, 0, 0), new(4, 0.4f, 0), new(7, 1, 0), new(10, 1.2f, 0), new(13, 1.2f, 0),
            new(16, 1.2f, 0), new(19, 1.2f, 0), new(22, 1.2f, 0), new(22, 1.2f, 4), new(22, 1.2f, 8),
            new(27, 1.2f, 8), new(28, 1.2f, 8)), [Snowball("snowball-pass", 16, 1.2f, 0)]);

    public static LevelDefinition IcicleCanyon() => new("winter-icicle", new(-1, 0.501f, 0), -7,
        Finish(new(31, 0.8f, 8), true),
        [Flat("start", 0, 0, 0, 4, 4), Between("press-runway", new(2, 0, 0), new(20, 0, 0), 2.8f),
         Flat("rest-one", 6, 0, 0, 3, 4), Flat("rest-two", 14, 0, 0, 3, 4),
         Flat("turn", 22, 0, 0, 4, 4), Between("ascent", new(22, 0, 2), new(22, 0.8f, 6), 1.3f),
         Flat("upper-turn", 22, 0.8f, 8, 4, 4), Between("ridge", new(24, 0.8f, 8), new(29, 0.8f, 8), 1.25f),
         Flat("finish-platform", 31, 0.8f, 8, 4, 3)],
        Coins(new(0, 0, 0), new(3, 0, 0), new(6, 0, 0), new(9, 0, 0), new(12, 0, 0),
            new(14, 0, 0), new(17, 0, 0), new(20, 0, 0), new(22, 0, 0), new(22, 0.4f, 4),
            new(26.5f, 0.8f, 8), new(29.5f, 0.8f, 8)),
        [IcePress("ice-press-one", 9, 0, 0), IcePress("ice-press-two", 17, 0, 0, 1.8f)]);

    public static LevelDefinition FrozenLake() => new("winter-lake", new(-1, 0.501f, 0), -7,
        Finish(new(10, 0.5f, 18.5f), true),
        [Flat("start", 0, 0, 0, 4, 4), Flat("ice-one", 5, 0, 0, 3, 2.8f, 1.7f),
         Flat("ice-two", 9.5f, 0.25f, 0, 3, 2.6f, 1.5f), Flat("ice-three", 14, 0.5f, 0, 3, 2.4f, 1.35f),
         Flat("solid-island", 18.7f, 0.5f, 0, 3.4f, 4),
         Between("bonus-neck", new(20.4f, 0.5f, 0), new(24, 0.5f, 0), 1.1f), Flat("bonus-island", 25.5f, 0.5f, 0, 3, 3),
         Between("turn-bridge", new(18.7f, 0.5f, 2), new(18.7f, 0.5f, 6), 1.2f), Flat("rest", 18.7f, 0.5f, 7.5f, 3, 3),
         Flat("ice-four", 18.7f, 0.5f, 11.8f, 2.4f, 2.8f, 1.5f),
         Flat("ice-five", 18.7f, 0.5f, 16, 2.4f, 2.8f, 1.35f),
         Flat("last-island", 18.7f, 0.5f, 20.4f, 4, 3.2f),
         Between("homeward", new(16.7f, 0.5f, 20.4f), new(11, 0.5f, 18.5f), 1.2f), Flat("finish-platform", 11, 0.5f, 18.5f, 4, 3)],
        Coins(new(0, 0, 0), new(5, 0, 0), new(9.5f, 0.25f, 0), new(14, 0.5f, 0), new(18.7f, 0.5f, 0),
            new(25, 0.5f, 0), new(26, 0.5f, 0), new(18.7f, 0.5f, 7.5f), new(18.7f, 0.5f, 11.8f),
            new(18.7f, 0.5f, 16), new(18.7f, 0.5f, 20.4f), new(12, 0.5f, 18.5f)), []);

    public static LevelDefinition AuroraSummit() => new("winter-aurora", new(-1, 0.501f, 0), -8,
        Finish(new(45.2f, 2.5f, 10), true),
        [Flat("start", 0, 0, 0, 4, 4), Between("climb-one", new(2, 0, 0), new(8, 1.2f, 0), 1.5f),
         Flat("first-rest", 10, 1.2f, 0, 4, 4), Between("snowball-ridge", new(12, 1.2f, 0), new(20, 1.2f, 0), 1.65f),
         Flat("turn", 22, 1.2f, 0, 4, 4), Between("climb-two", new(22, 1.2f, 2), new(22, 2.5f, 8), 1.35f),
         Flat("upper-rest", 22, 2.5f, 10, 4, 4), Between("press-bridge", new(24, 2.5f, 10), new(34, 2.5f, 10), 2.4f),
         Flat("takeoff", 35.5f, 2.5f, 10, 3, 3), Flat("last-ice", 40, 2.5f, 10, 3, 2.8f, 1.65f),
         Flat("finish-platform", 45.8f, 2.5f, 10, 5, 4)],
        Coins(new(0, 0, 0), new(5, 0.6f, 0), new(10, 1.2f, 0), new(13, 1.2f, 0), new(16, 1.2f, 0),
            new(22, 1.2f, 0), new(22, 1.85f, 5), new(22, 2.5f, 10), new(26, 2.5f, 10),
            new(29, 2.5f, 10), new(35.5f, 2.5f, 10), new(40, 2.5f, 10)),
        [Snowball("snowball-summit", 16, 1.2f, 0, 0.7f), IcePress("ice-press-summit", 29, 2.5f, 10, 1)]);
}
