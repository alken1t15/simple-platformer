using Platformer.Core;

namespace Platformer.App;

/// <summary>Каталог поставляемых локаций, общий для Program и интеграционных проверок.</summary>
public static class GameLevels
{
    public const string FinalLevelId = "sky-storm";
    public static bool IsBaseLevel(string id) => id is "level1" or "level2" or "jump-circuit" or "island-circuit";
    public static LevelCatalog CreateCatalog() => new([
        TrainingTrack.Create(), TurningMaze.Create(), JumpCircuit.Create(), IslandCircuit.Create(),
        CoralAscent.Create(), WindmillPass.Create(), LavaCauseway.Create(), CrumbleCanyon.Create(), TempleGauntlet.Create(),
        PendulumGorge.Create(), PistonFoundry.Create(), ClockworkSummit.Create(), SkyStorm.Create()]);
}
