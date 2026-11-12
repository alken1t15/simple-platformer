using Engine3D.Core;

namespace Platformer.App;

/// <summary>Авторская оценка маршрута для меню, не настройка игровой физики.</summary>
internal enum LevelDifficulty { Easy, Normal, Hard, VeryHard, Expert }

internal static class LevelDifficulties
{
    internal static LevelDifficulty For(string id) => id switch
    {
        "level1" => LevelDifficulty.Easy,
        "level2" or "jump-circuit" or "island-circuit" or "coral-ascent" or "windmill-pass" or "lava-causeway" => LevelDifficulty.Normal,
        "crumble-canyon" or "temple-gauntlet" or "pendulum-gorge" or "piston-foundry" => LevelDifficulty.Hard,
        "clockwork-summit" or "sky-storm" => LevelDifficulty.VeryHard,
        _ => LevelDifficulty.Normal,
    };

    internal static string Label(LevelDifficulty difficulty) => "Difficulty: " + (difficulty == LevelDifficulty.VeryHard ? "Very hard" : difficulty.ToString());
    internal static ColorRgba Color(LevelDifficulty difficulty) => difficulty switch
    {
        LevelDifficulty.Easy => new(0.40f, 0.92f, 0.57f),
        LevelDifficulty.Normal => new(0.98f, 0.86f, 0.38f),
        LevelDifficulty.Hard => new(1f, 0.67f, 0.32f),
        LevelDifficulty.VeryHard => new(1f, 0.47f, 0.28f),
        _ => new(1f, 0.34f, 0.43f),
    };
}
