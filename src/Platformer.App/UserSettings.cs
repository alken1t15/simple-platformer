namespace Platformer.App;

/// <summary>Настройки процесса; LevelFlow сохраняет этот экземпляр между попытками и уровнями.</summary>
public sealed class UserSettings
{
    private float masterVolume = 1f;
    private string skinId = "classic-blue";
    private string musicTrackId = MusicTracks.OriginalId;

    public string MusicTrackId
    {
        get => musicTrackId;
        set { MusicTracks.Get(value); musicTrackId = value; }
    }

    public float MasterVolume
    {
        get => masterVolume;
        set
        {
            if (!float.IsFinite(value) || value < 0f || value > 1f)
                throw new ArgumentOutOfRangeException(nameof(value));
            masterVolume = value;
        }
    }
    public bool Muted { get; set; }
    public string SkinId
    {
        get => skinId;
        set
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            GameSkins.Get(value);
            skinId = value;
        }
    }
}
