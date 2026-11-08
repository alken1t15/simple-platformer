namespace Platformer.App;

public sealed record MusicTrack(string Id, string DisplayName, string FileName);

/// <summary>Оба трека поставляются с игрой; пути не берутся из пользовательского ввода.</summary>
public static class MusicTracks
{
    public const string OriginalId = "original";
    public const string ExplorationId = "exploration";
    public static IReadOnlyList<MusicTrack> All { get; } = Array.AsReadOnly(new[]
    {
        new MusicTrack(OriginalId, "Original", "music.wav"),
        new MusicTrack(ExplorationId, "Exploration Theme", "exploration-theme.wav"),
    });

    public static MusicTrack Get(string id) => All.FirstOrDefault(track => track.Id == id)
        ?? throw new ArgumentException("Неизвестный музыкальный трек", nameof(id));

    public static string Cycle(string id, int direction)
    {
        int index = All.ToList().IndexOf(Get(id));
        return All[(index + Math.Sign(direction) + All.Count) % All.Count].Id;
    }
}
