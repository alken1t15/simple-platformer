using System.Numerics;
using static Platformer.Core.ChallengeGeometry;
using static Platformer.Core.ExpertGeometry;

namespace Platformer.Core;

/// <summary>Три поперечных толкателя, верхняя секция и две короткоживущие ступени.</summary>
public static class PistonFoundry
{
    public static LevelDefinition Create() => new("piston-foundry", new(-1, 0.501f, 0), -6,
        Finish(new(34, 1.2f, 9), true),
        [Flat("start", 0, 0, 0, 4, 4), Between("piston-runway", new(2, 0, 0), new(18, 0, 0), 2),
         Flat("waiting-island", 20, 0, 0, 4, 4), Between("climb", new(20, 0, 2), new(20, 1.2f, 7), 1.35f),
         Flat("upper-island", 20, 1.2f, 9, 4, 4),
         Flat("crumb-one", 25.2f, 1.2f, 9, 2.8f, 2, 1.25f),
         Flat("crumb-two", 29.4f, 1.2f, 9, 2.8f, 2, 1.15f),
         Flat("finish-platform", 34, 1.2f, 9, 3.6f, 3.2f)],
        Coins(new(0, 0, 0), new(3, 0, 0), new(6, 0, 0), new(10, 0, 0), new(14, 0, 0), new(18, 0, 0),
            new(20, 0, 0), new(20, 0.6f, 4.5f), new(20, 1.2f, 9), new(25.2f, 1.2f, 9),
            new(29.4f, 1.2f, 9), new(34, 1.2f, 9)),
        [Piston("piston-one", 6, 0, 0), Piston("piston-two", 11, 0, 0, 1.8f), Piston("piston-three", 16, 0, 0, 3.6f)]);
}
