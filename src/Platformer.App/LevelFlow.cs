using Platformer.Core;

namespace Platformer.App;

public enum AppScreen { MainMenu, LevelSelect, Settings, SkinSelect, Playing, Paused, Failed, LevelResult, GameComplete }

/// <summary>Сохранённый результат завершённой локации текущего маршрута.</summary>
public readonly record struct LevelResult(string LevelId, int CoinCount, int TotalCoins);

/// <summary>
/// CPU-модель переходов. Адаптеры #6/#19/#21 подключают очистку input/runner,
/// остановку gameplay audio и pause/resume audio через callbacks; окна здесь нет.
/// </summary>
public sealed class LevelFlow
{
    private readonly LevelCatalog catalog;
    private readonly SimulationSettings settings;
    private readonly Action resetTransientState;
    private readonly Action stopGameplayAudio;
    private readonly Action<bool> setAudioPaused;
    private GameSession? session;
    private AppScreen returnScreen = AppScreen.MainMenu;
    private readonly List<LevelResult> completed = [];

    public UserSettings UserSettings { get; }
    public AppScreen Screen { get; private set; } = AppScreen.MainMenu;
    public SessionSnapshot? Snapshot => session?.Snapshot;
    public GameScenePresenter? Presenter { get; private set; }
    public IReadOnlyList<LevelResult> CompletedLevels => Array.AsReadOnly(completed.ToArray());
    public int TotalCoinCount => completed.Sum(result => result.CoinCount) + (Snapshot?.CoinCount ?? 0);
    public event Action? GameCompleted;

    public LevelFlow(LevelCatalog catalog, SimulationSettings settings, UserSettings userSettings,
        Action? resetTransientState = null, Action? stopGameplayAudio = null, Action<bool>? setAudioPaused = null)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(userSettings);
        this.catalog = catalog;
        this.settings = settings;
        UserSettings = userSettings;
        this.resetTransientState = resetTransientState ?? (() => { });
        this.stopGameplayAudio = stopGameplayAudio ?? (() => { });
        this.setAudioPaused = setAudioPaused ?? (_ => { });
    }

    public void Start(string levelId)
    {
        var (next, presenter) = Prepare(catalog.Get(levelId));
        ResetForTransition();
        completed.Clear();
        session = next;
        Presenter = presenter;
        Screen = AppScreen.Playing;
    }

    public void Restart()
    {
        Require(AppScreen.Playing, AppScreen.Paused, AppScreen.Failed, AppScreen.LevelResult);
        var next = new GameSession(catalog.Get(session!.Snapshot.LevelId), settings);
        ResetForTransition();
        session = next;
        Presenter!.Apply(next.Snapshot, GameSkins.Get(UserSettings.SkinId));
        Screen = AppScreen.Playing;
    }

    public void Next()
    {
        Require(AppScreen.LevelResult);
        var current = session!.Snapshot;
        string? nextId = catalog.GetNextId(current.LevelId);
        var next = nextId is null ? default : Prepare(catalog.Get(nextId));
        var result = new LevelResult(current.LevelId, current.CoinCount, catalog.Get(current.LevelId).Coins.Count);
        ResetForTransition();
        completed.Add(result);
        session = next.Session;
        Presenter = next.Presenter;
        Screen = nextId is null ? AppScreen.GameComplete : AppScreen.Playing;
        if (Screen == AppScreen.GameComplete) GameCompleted?.Invoke();
    }

    public void Pause()
    {
        if (Screen == AppScreen.Paused) return;
        Require(AppScreen.Playing);
        resetTransientState();
        setAudioPaused(true);
        Screen = AppScreen.Paused;
    }

    public void Continue()
    {
        Require(AppScreen.Paused);
        resetTransientState();
        setAudioPaused(false);
        Screen = AppScreen.Playing;
    }

    public void OpenLevelSelect()
    {
        Require(AppScreen.MainMenu, AppScreen.Failed, AppScreen.GameComplete);
        OpenMenu(AppScreen.LevelSelect);
    }

    public void OpenSettings()
    {
        Require(AppScreen.MainMenu, AppScreen.Paused);
        OpenMenu(AppScreen.Settings);
    }

    public void OpenSkins()
    {
        Require(AppScreen.MainMenu);
        OpenMenu(AppScreen.SkinSelect);
    }

    /// <summary>Settings сохраняет паузу; Escape в MainMenu не закрывает приложение.</summary>
    public void Back()
    {
        switch (Screen)
        {
            case AppScreen.MainMenu: return;
            case AppScreen.LevelSelect:
            case AppScreen.Settings:
            case AppScreen.SkinSelect:
                resetTransientState();
                Screen = returnScreen;
                return;
            case AppScreen.Paused: Continue(); return;
            case AppScreen.Failed:
            case AppScreen.LevelResult:
            case AppScreen.GameComplete: MainMenu(); return;
            default: throw new InvalidOperationException($"Возврат недоступен из {Screen}");
        }
    }

    private void OpenMenu(AppScreen screen)
    {
        // Не сбрасываем audio pause при переходе Paused -> Settings -> Paused.
        resetTransientState();
        returnScreen = Screen;
        Screen = screen;
    }

    /// <summary>Потеря фокуса приостанавливает Playing; активация окна сама игру не продолжает.</summary>
    public void LoseFocus()
    {
        if (Screen == AppScreen.Playing) Pause();
        else resetTransientState();
    }

    public void MainMenu()
    {
        ResetForTransition();
        session = null;
        Presenter = null;
        completed.Clear();
        Screen = AppScreen.MainMenu;
    }

    public void Step(GameCommand command)
    {
        if (Screen != AppScreen.Playing) return;
        session!.Step(command);
        Presenter!.Apply(session.Snapshot, GameSkins.Get(UserSettings.SkinId), session.LastPath);
        AppScreen next = session.Snapshot.Phase switch
        {
            SessionPhase.Failed => AppScreen.Failed,
            SessionPhase.LevelComplete => AppScreen.LevelResult,
            _ => AppScreen.Playing,
        };
        Screen = next;
        if (next != AppScreen.Playing) ResetForTransition();
    }

    public IReadOnlyList<GameEvent> DrainEvents() => session?.DrainEvents() ?? Array.Empty<GameEvent>();

    private (GameSession Session, GameScenePresenter Presenter) Prepare(LevelDefinition level)
    {
        var next = new GameSession(level, settings);
        var presenter = new GameScenePresenter(level);
        presenter.Apply(next.Snapshot, GameSkins.Get(UserSettings.SkinId));
        return (next, presenter);
    }

    private void ResetForTransition()
    {
        resetTransientState();
        stopGameplayAudio();
        setAudioPaused(false);
    }

    private void Require(params AppScreen[] allowed)
    {
        if (!allowed.Contains(Screen)) throw new InvalidOperationException($"Переход недоступен из {Screen}");
    }
}
