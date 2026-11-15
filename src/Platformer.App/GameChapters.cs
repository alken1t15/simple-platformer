namespace Platformer.App;

public enum GameChapter { Tropical, Winter }

/// <summary>Группировка данных App; ID и формат сохранения прежних уровней не меняются.</summary>
public static class GameChapters
{
    public static GameChapter For(string levelId) => levelId is "winter-snowbound" or "winter-icicle" or "winter-lake" or "winter-aurora" or "winter-crystal" or "winter-gifts" or "winter-midnight"
        ? GameChapter.Winter : GameChapter.Tropical;
    public static string Name(GameChapter chapter) => chapter == GameChapter.Winter ? "Chapter 2 - Winter Lights" : "Chapter 1 - Tropical Skies";
}
