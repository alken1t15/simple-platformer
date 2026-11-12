using System.Numerics;
using static Platformer.Core.ChallengeGeometry;
using static Platformer.Core.ExpertGeometry;

namespace Platformer.Core;

/// <summary>Комбинация маятников, толкателя, диагоналей, барьера и прыжков на высоте.</summary>
public static class ClockworkSummit
{
    public static LevelDefinition Create() => new("clockwork-summit", new(-1, 0.501f, 0), -7,
        Finish(new(40.2f, 2.2f, 12), true),
        [Flat("start", 0, 0, 0, 4, 4), Between("climb", new(2, 0, 0), new(7, 1.2f, 0), 1.25f),
         Flat("first-rest", 9, 1.2f, 0, 4, 4), Between("pendulum-bridge", new(11, 1.2f, 0), new(20, 1.2f, 0), 1.4f),
         Flat("second-rest", 22, 1.2f, 0, 4, 4),
         Between("diagonal", new(22, 1.2f, 2), new(27, 1.2f, 7), 1),
         Flat("upper-turn", 27, 1.2f, 8.5f, 3, 3), Between("piston-bridge", new(28.5f, 1.2f, 8.5f), new(35.5f, 1.2f, 8.5f), 1.7f),
         Flat("hurdle-island", 38.5f, 1.2f, 8.5f, 6, 4),
         Flat("crumb-step", 44.5f, 1.7f, 8.5f, 2.8f, 2.4f, 1.4f),
         Flat("last-landing", 49.1f, 2.2f, 8.5f, 3.2f, 3, 1.6f),
         Between("last-diagonal", new(49.1f, 2.2f, 10), new(45.1f, 2.2f, 12), 1.05f),
         Between("finish-neck", new(45.1f, 2.2f, 12), new(42.2f, 2.2f, 12), 1.05f),
         Flat("finish-platform", 40.2f, 2.2f, 12, 4, 3), ..Gantry("summit-arch", 15.5f, 1.2f, 0)],
        Coins(new(0, 0, 0), new(4.5f, 0.6f, 0), new(9, 1.2f, 0), new(15.5f, 1.2f, 0),
            new(22, 1.2f, 0), new(24.5f, 1.2f, 4.5f), new(27, 1.2f, 8.5f), new(32, 1.2f, 8.5f),
            new(38.5f, 1.75f, 8.5f), new(44.5f, 1.7f, 8.5f), new(49.1f, 2.2f, 8.5f), new(40.7f, 2.2f, 12)),
        [Pendulum("summit-stone", 15.5f, 1.2f, 0, 0.7f), Piston("summit-piston", 32, 1.2f, 8.5f, 1.3f),
         Hurdle("summit-hurdle", new(38.5f, 1.2f, 8.5f), 4.2f)]);
}
