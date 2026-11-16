namespace Platformer.App;

/// <summary>Настройки сеанса; обычный запуск загружает/сохраняет их через SettingsStore.</summary>
public sealed class UserSettings
{
    private float musicVolume=1,effectsVolume=1;
    public float MusicVolume { get=>musicVolume; set=>musicVolume=Gain(value); }
    public float EffectsVolume { get=>effectsVolume; set=>effectsVolume=Gain(value); }
    private static float Gain(float value)=>float.IsFinite(value)&&value is >=0 and <=1?value:throw new ArgumentOutOfRangeException(nameof(value));
    public bool ReducedEffects { get; set; }
    public bool FlashingEnabled { get; set; } = true;
    private ControlBindings bindings=new();
    public ControlBindings Bindings { get=>bindings; set=>bindings=value is {IsValid:true}?value:throw new ArgumentException("Недопустимая раскладка",nameof(value)); }
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
