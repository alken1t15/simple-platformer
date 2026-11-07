using Platformer.Core;

namespace Platformer.App;

/// <summary>Каталог поставляемых локаций, общий для Program и интеграционных проверок.</summary>
public static class GameLevels
{
    public static LevelCatalog CreateCatalog() => new([TrainingTrack.Create(), TurningMaze.Create()]);
}
