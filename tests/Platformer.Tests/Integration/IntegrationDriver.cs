using System.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Platformer.App;
using Platformer.Core;

namespace Platformer.Tests.Integration;

/// <summary>
/// Audio double для стыка AudioCoordinator → backend: фиксирует каждый voice и закрытие playback.
/// Слышимость не проверяет — её подтверждает отдельный native Linux прогон.
/// </summary>
internal sealed class RecordingPlayback : IGameAudioPlayback
{
    internal sealed class Voice(string clip) : IGameAudioVoice
    {
        public string Clip { get; } = clip;
        public bool Paused { get; private set; }
        public bool Disposed { get; private set; }
        public void Pause() { Assert.IsFalse(Disposed, $"Pause после Dispose: {Clip}"); Paused = true; }
        public void Resume() { Assert.IsFalse(Disposed, $"Resume после Dispose: {Clip}"); Paused = false; }
        public void Dispose() => Disposed = true;
    }

    public List<Voice> Music { get; } = [];
    public List<Voice> Effects { get; } = [];
    public bool Disposed { get; private set; }
    public IEnumerable<Voice> All => Music.Concat(Effects);
    public IEnumerable<Voice> Live => All.Where(voice => !voice.Disposed);

    public IGameAudioVoice PlayMusic(string trackId = MusicTracks.OriginalId)
    {
        Assert.IsFalse(Disposed, "PlayMusic после Dispose");
        var voice = new Voice("music");
        Music.Add(voice);
        return voice;
    }

    public IGameAudioVoice PlayEffect(GameEventKind kind)
    {
        Assert.IsFalse(Disposed, "PlayEffect после Dispose");
        var voice = new Voice(kind.ToString());
        Effects.Add(voice);
        return voice;
    }

    public int Count(GameEventKind kind) => Effects.Count(voice => voice.Clip == kind.ToString());
    public void Dispose() => Disposed = true;
}

/// <summary>
/// Искусственные ввод и часы поверх настоящего GameplayHost: InputRouter, FixedStepRunner,
/// MenuController, LevelFlow, GameSession, Presenter и AudioCoordinator без окна.
/// </summary>
internal sealed class IntegrationDriver
{
    public const string First = "level1";
    public const string Second = "level2";

    public RecordingPlayback Playback { get; } = new();
    public AudioCoordinator Audio { get; }
    public GameplayHost Host { get; }
    public List<float> AppliedVolumes { get; } = [];
    public double FixedStep => Host.Frames.Clock.FixedStepSeconds;
    public LevelFlow Flow => Host.Flow;
    public AppScreen Screen => Host.Flow.Screen;
    public SessionSnapshot Snapshot => Host.Flow.Snapshot ?? throw new AssertFailedException("Нет активного сеанса");

    public IntegrationDriver(LevelCatalog? catalog = null, UserSettings? settings = null)
    {
        Audio = new AudioCoordinator(Playback);
        Host = new GameplayHost(catalog ?? TwoLevels(), SimulationSettings.Default, settings ?? new UserSettings(),
            applyMasterVolume: AppliedVolumes.Add, audio: Audio);
    }

    /// <summary>
    /// level1: монета на spawn (собирается в tick 1), вторая вне маршрута, финиш справа (D).
    /// level2: финиш справа (D), неподвижная балка слева (A ведёт к Failed).
    /// </summary>
    public static LevelCatalog TwoLevels()
    {
        var first = SessionFixtures.Level(First, finishX: 2f, coins:
        [
            new CoinDefinition("spawn-coin", new(0f, 0.5f, 0f), 0.2f),
            new CoinDefinition("far-coin", new(0f, 0.5f, 8f), 0.2f),
        ]);
        var second = SessionFixtures.WithHazards(SessionFixtures.Level(Second, finishX: 2f),
            SessionFixtures.Beam(pivot: new Vector3(-3.5f, 0.501f, 0f)));
        return new LevelCatalog([first, second]);
    }

    public GameplayFrameResult Frame(AppButtons down = AppButtons.None, AppButtons pressed = AppButtons.None,
        double seconds = 0d, Vector2? pointer = null, bool focused = true, AppButtons released = AppButtons.None) =>
        Host.Advance(new AppInputSnapshot(focused, down | pressed, pressed, default, pointer ?? default, released), seconds);

    /// <summary>Отпущено → фронт нажатия → отпускание: как отдельное нажатие пользователя.</summary>
    public void Press(AppButtons key)
    {
        Frame();
        Frame(pressed: key);
        Frame(released: key);
    }

    public Vector2 CenterOf(string controlId)
    {
        var bounds = Host.View.Controls.SingleOrDefault(control => control.Id == controlId)?.Bounds
            ?? throw new AssertFailedException($"Нет control '{controlId}' на {Screen}: "
                + string.Join(", ", Host.View.Controls.Select(control => control.Id)));
        return new(bounds.X + bounds.Width / 2f, bounds.Y + bounds.Height / 2f);
    }

    /// <summary>Клик по control; extra добавляется в тот же кадр (например, двойной Enter+click).</summary>
    public void Click(string controlId, AppButtons extra = AppButtons.None)
    {
        Frame();
        var point = CenterOf(controlId);
        Frame(pointer: point);
        Frame(pressed: AppButtons.PointerLeft | extra, pointer: point);
        Frame(released: AppButtons.PointerLeft | extra, pointer: point);
    }

    /// <summary>Удерживает клавиши по одному fixed tick за кадр, пока сеанс не выйдет из Playing.</summary>
    public void HoldUntilLeavesPlaying(AppButtons keys, int maximumTicks = 600)
    {
        Assert.AreEqual(AppScreen.Playing, Screen);
        for (int i = 0; i < maximumTicks && Screen == AppScreen.Playing; i++)
            Frame(down: keys, seconds: FixedStep);
        Assert.AreNotEqual(AppScreen.Playing, Screen, $"Сеанс не завершился за {maximumTicks} ticks");
        Frame(released: keys);
    }

    public string HudText(string prefix) => Host.View.Texts.Select(text => text.Text)
        .SingleOrDefault(text => text.StartsWith(prefix, StringComparison.Ordinal))
        ?? throw new AssertFailedException($"Нет HUD-текста '{prefix}' на {Screen}");
}
