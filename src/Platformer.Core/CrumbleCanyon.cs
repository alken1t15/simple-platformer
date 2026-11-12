using System.Numerics;
using static Platformer.Core.ChallengeGeometry;

namespace Platformer.Core;

/// <summary>Две цепочки временных опор с постоянными площадками для передышки между ними.</summary>
public static class CrumbleCanyon
{
    public static LevelDefinition Create() => new("crumble-canyon", new(-1, 0.501f, 0), -6,
        Finish(new(10.2f, 0.5f, 19.4f), true),
        [
            Flat("start", 0, 0, 0, 4, 4),
            Flat("crumb-one", 4.8f, 0, 0, 2.8f, 2.6f, 1.65f),
            Flat("crumb-two", 9, 0.15f, 0, 2.8f, 2.6f, 1.65f),
            Flat("crumb-three", 13.2f, 0, 0, 2.8f, 2.6f, 1.65f),
            Flat("rest-island", 17.6f, 0, 0, 3.2f, 4),
            Between("bonus-bridge", new(19.2f, 0, 0), new(22.4f, 0, 0), 1.05f),
            Flat("bonus-island", 24, 0, 0, 3.2f, 3),
            Between("turn-bridge", new(17.6f, 0, 2), new(17.6f, 0, 5), 1.1f),
            Flat("turn-island", 17.6f, 0, 6.5f, 3, 3),
            Flat("crumb-four", 17.6f, 0.25f, 10.8f, 2.2f, 2.8f, 1.45f),
            Flat("crumb-five", 17.6f, 0.5f, 15, 2.2f, 2.8f, 1.3f),
            Flat("landing", 17.6f, 0.5f, 19.4f, 4, 3.2f),
            Flat("finish-platform", 12.6f, 0.5f, 19.4f, 6, 2.4f),
        ],
        Coins(new(0.2f, 0, 0), new(4.8f, 0, 0), new(9, 0.15f, 0), new(13.2f, 0, 0),
            new(17.6f, 0, 0), new(23.6f, 0, 0), new(24.5f, 0, 0), new(17.6f, 0, 6.5f),
            new(17.6f, 0.25f, 10.8f), new(17.6f, 0.5f, 15), new(17.6f, 0.5f, 19.4f), new(11.1f, 0.5f, 19.4f)), []);
}
