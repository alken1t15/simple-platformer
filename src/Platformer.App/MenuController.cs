using System.Numerics;
using Platformer.Core;

namespace Platformer.App;

public readonly record struct MenuUpdateResult(bool ResetCamera, bool ResetInput = false);

/// <summary>
/// CPU-модель #19. Получает уже маршрутизированный InputFrame; окно/курсор/Step остаются у host #6.
/// Все переходы выполняет существующий LevelFlow. Callback громкости вызывается сразу и в первом меню.
/// </summary>
public sealed class MenuController
{
    private readonly LevelFlow flow;
    private readonly LevelCatalog catalog;
    private readonly Action<float> applyMasterVolume;
    private readonly bool visual;
    private AppScreen observedScreen;
    private int selectedIndex;
    private bool confirmingMainMenu;
    private bool showingControls;
    private int controlsReturnSelection;
    private int returnSelection;
    private bool resetInputRequested;
    private Vector2? pointer;
    private Vector2? lastPointerPosition;
    private string? pressedId;
    private bool focused = true;

    public bool CloseRequested { get; private set; }
    public WindowSettings Window { get; } = new();
    private float EffectiveVolume => flow.UserSettings.Muted ? 0f : flow.UserSettings.MasterVolume;

    public MenuController(LevelFlow flow, LevelCatalog catalog, Action<float>? applyMasterVolume = null, bool visual = false)
    {
        ArgumentNullException.ThrowIfNull(flow);
        ArgumentNullException.ThrowIfNull(catalog);
        this.flow = flow;
        this.catalog = catalog;
        this.visual = visual;
        this.applyMasterVolume = applyMasterVolume ?? (_ => { });
        observedScreen = flow.Screen;
        ApplyVolume();
    }

    public MenuView BuildView(float width, float height)
    {
        SynchronizeScreen();
        var snapshot = flow.Snapshot;
        HudModel? hud = flow.Screen == AppScreen.Playing && snapshot is not null
            ? HudModel.Create(catalog.Get(snapshot.LevelId), snapshot) : null;
        return MenuLayout.Build(flow.Screen, Page(), hud, EffectiveVolume, selectedIndex,
            focused ? pointer : null, pressedId, width, height, visual);
    }

    /// <summary>Первый Normal snapshot после capture/focus задаёт базу, а не движение выбора.</summary>
    public void ResetPointerPosition(Vector2 position)
    {
        lastPointerPosition = float.IsFinite(position.X) && float.IsFinite(position.Y) ? position : null;
        pointer = null;
    }

    /// <summary>
    /// Один вызов после маршрутизации кадра. Mouse target приоритетнее Enter; за кадр одно действие.
    /// После перехода host синхронизирует runner с Flow.Screen и очищает удержанные клавиши.
    /// </summary>
    public MenuUpdateResult Handle(InputFrame input, float width, float height, bool isFocused = true)
    {
        MenuLayout.ValidateSize(width, height);
        SynchronizeScreen();
        pressedId = null;
        if (CloseRequested) return default;
        if (!isFocused)
        {
            if (focused) flow.LoseFocus();
            focused = false;
            pointer = null;
            SynchronizeScreen();
            return default;
        }
        focused = true;
        if (input.PausePressed && flow.Screen == AppScreen.Playing)
        {
            flow.Pause();
            SynchronizeScreen();
            return default;
        }
        if (!confirmingMainMenu && input.RestartPressed && flow.Screen is AppScreen.Playing or AppScreen.Paused or AppScreen.Failed)
        {
            flow.Restart();
            SynchronizeScreen();
            return new(true);
        }
        if (flow.Screen == AppScreen.Playing) return default;
        var menu = input.Menu;
        if (menu.BackPressed)
        {
            bool resetInput = false;
            if (showingControls) { ShowControls(false); resetInput = true; }
            else if (confirmingMainMenu) { CancelMainMenu(); resetInput = true; }
            else if (flow.Screen is AppScreen.Failed or AppScreen.LevelResult)
            { RequestMainMenu(); resetInput = true; }
            else flow.Back();
            resetInputRequested = false;
            SynchronizeScreen();
            return new(false, resetInput);
        }

        var view = BuildView(width, height);
        if (view.Controls.Count == 0) return default; // Минимизированное окно не получает клики.
        // Смена экрана очищает hover, но не превращает неподвижный указатель в движение.
        bool pointerMoved = lastPointerPosition != menu.PointerPosition;
        pointer = float.IsFinite(menu.PointerPosition.X) && float.IsFinite(menu.PointerPosition.Y)
            ? menu.PointerPosition : null;
        lastPointerPosition = pointer;
        int hit = -1;
        for (int i = 0; i < view.Controls.Count; i++)
            if (pointer is { } position && view.Controls[i].Bounds.Contains(position)) { hit = i; break; }
        if (menu.VerticalStep != 0)
            selectedIndex = (selectedIndex + Math.Sign(menu.VerticalStep) + view.Controls.Count) % view.Controls.Count;
        if (hit >= 0 && (menu.PointerPressed || (pointerMoved && menu.VerticalStep == 0))) selectedIndex = hit;

        var options = Page().Options;
        bool mouseActivation = menu.PointerPressed && hit >= 0;
        var option = options[mouseActivation ? hit : selectedIndex];
        if (mouseActivation || menu.ActivatePressed)
        {
            pressedId = option.Id;
            var control = view.Controls[selectedIndex];
            bool resetCamera = Execute(option, mouseActivation ? pointer : null, control.ValueBounds ?? control.Bounds);
            SynchronizeScreen();
            var result = new MenuUpdateResult(resetCamera, resetInputRequested);
            resetInputRequested = false;
            return result;
        }
        if (menu.HorizontalStep != 0 && option.Action == MenuAction.Volume)
            SetVolume(EffectiveVolume + Math.Sign(menu.HorizontalStep) * 0.05f);
        if (menu.HorizontalStep != 0 && option.Action == MenuAction.Resolution)
            Window.Cycle(menu.HorizontalStep);
        if (menu.HorizontalStep != 0 && option.Action == MenuAction.DisplayMode)
            Window.ToggleMode();
        if (menu.HorizontalStep != 0 && option.Action == MenuAction.Music)
            flow.UserSettings.MusicTrackId = MusicTracks.Cycle(flow.UserSettings.MusicTrackId, menu.HorizontalStep);
        return default;
    }

    private bool Execute(MenuOption option, Vector2? click, MenuRect bounds)
    {
        if (!option.Enabled) return false;
        switch (option.Action)
        {
            case MenuAction.Music:
                flow.UserSettings.MusicTrackId = MusicTracks.Cycle(flow.UserSettings.MusicTrackId, 1);
                break;
            case MenuAction.DisplayMode: Window.ToggleMode(); break;
            case MenuAction.Resolution: Window.Cycle(1); break;
            case MenuAction.ApplyResolution: Window.RequestApply(); break;
            case MenuAction.Play: flow.Start(catalog.Levels[0].Id); return true;
            case MenuAction.Restart: flow.Restart(); return true;
            case MenuAction.Next: flow.Next(); return true;
            case MenuAction.Continue: flow.Continue(); break;
            case MenuAction.Levels: flow.OpenLevelSelect(); break;
            case MenuAction.Settings: flow.OpenSettings(); break;
            case MenuAction.Skins: flow.OpenSkins(); break;
            case MenuAction.Controls: ShowControls(true); break;
            case MenuAction.Back:
                if (showingControls) ShowControls(false);
                else flow.Back();
                break;
            case MenuAction.MainMenu: RequestMainMenu(); break;
            case MenuAction.CancelMainMenu: CancelMainMenu(); break;
            case MenuAction.ConfirmMainMenu:
                confirmingMainMenu = false;
                flow.MainMenu();
                break;
            case MenuAction.Exit: CloseRequested = true; break;
            case MenuAction.ChooseLevel: flow.Start(option.Value!); return true;
            case MenuAction.ChooseSkin:
                if (flow.Progress.IsSkinUnlocked(option.Value!)) flow.UserSettings.SkinId = option.Value!;
                break;
            case MenuAction.Volume:
                if (click is { } point) SetVolume((point.X - bounds.X) / bounds.Width);
                break;
            case MenuAction.Mute:
                flow.UserSettings.Muted = !flow.UserSettings.Muted;
                ApplyVolume();
                break;
        }
        return false;
    }

    private void SetVolume(float value)
    {
        value = Math.Clamp(MathF.Round(value * 20f) / 20f, 0f, 1f);
        if (flow.UserSettings.Muted && value == 0f) return; // Нулевая шкала не изменилась; сохраняем громкость для Unmute.
        flow.UserSettings.Muted = false;
        flow.UserSettings.MasterVolume = value;
        ApplyVolume();
    }

    private void ApplyVolume() => applyMasterVolume(EffectiveVolume);

    private void ShowControls(bool show)
    {
        if (show) controlsReturnSelection = selectedIndex;
        showingControls = show;
        selectedIndex = show ? 0 : controlsReturnSelection;
        pointer = null;
        pressedId = null;
        resetInputRequested = true;
    }

    private void RequestMainMenu()
    {
        if (flow.Snapshot is null) { flow.MainMenu(); return; }
        returnSelection = selectedIndex;
        confirmingMainMenu = true;
        selectedIndex = 0; // Безопасный выбор — Cancel, не потеря текущей попытки.
        pointer = null;
        pressedId = null;
        resetInputRequested = true;
    }

    private void CancelMainMenu()
    {
        confirmingMainMenu = false;
        selectedIndex = returnSelection;
        pointer = null;
        pressedId = null;
        resetInputRequested = true;
    }

    private void SynchronizeScreen()
    {
        if (flow.Screen == observedScreen) return;
        observedScreen = flow.Screen;
        selectedIndex = 0;
        confirmingMainMenu = false;
        showingControls = false;
        pointer = null;
        pressedId = null;
        if (flow.Screen == AppScreen.Settings) Window.SelectActual();
    }

    private MenuPage Page()
    {
        MenuOption Button(string id, string label, MenuAction action) => new(id, label, action);
        MenuOption Back() => Button("back", "Back", MenuAction.Back);
        MenuOption Main() => Button("main-menu", "Main menu", MenuAction.MainMenu);
        MenuOption Retry() => Button("retry", "Retry", MenuAction.Restart);
        var settings = flow.UserSettings;
        if (showingControls && flow.Screen == AppScreen.Settings)
            return new("Controls", ["WASD - roll / move", "Mouse - rotate camera",
                "Space - jump (hold to repeat)", "Shift - short boost", "R - restart level",
                "Esc - pause / back", "Up / Down, Tab / Shift+Tab - menu",
                "Left / Right - change setting", "Enter / Space / Mouse click - select"], [Back()], IsControlsPage: true);
        if (confirmingMainMenu)
            return new("Leave this run?", ["Best results stay. This run will end."],
                [Button("cancel-main-menu", "Cancel", MenuAction.CancelMainMenu),
                 Button("confirm-main-menu", "Leave to main menu", MenuAction.ConfirmMainMenu)]);
        return flow.Screen switch
        {
            AppScreen.MainMenu => new("Ball Adventure", [$"Adventure stars: {flow.Progress.TotalStars}/{flow.Progress.MaximumStars}"],
            [Button("play", "Play", MenuAction.Play), Button("levels", "Levels", MenuAction.Levels),
             Button("settings", "Settings", MenuAction.Settings), Button("skins", "Skins", MenuAction.Skins),
             Button("exit", "Exit", MenuAction.Exit)]),
            AppScreen.LevelSelect => new("Choose a level", [$"Sky Storm: {Math.Min(12, flow.Progress.BaseStars)}/12 stars on levels 1-4"],
                catalog.Levels.Select((level, index) => new MenuOption($"level:{level.Id}",
                    $"{index + 1}. {HudModel.LevelNameFor(level.Id)}",
                    MenuAction.ChooseLevel, MenuControlKind.Level, level.Id,
                    Enabled: flow.Progress.IsLevelUnlocked(level.Id), Detail: LevelDifficulties.Label(LevelDifficulties.For(level.Id)),
                    Locked: !flow.Progress.IsLevelUnlocked(level.Id), Rating: flow.Progress.StarsFor(level.Id),
                    Difficulty: LevelDifficulties.For(level.Id)))
                .Concat([Back()]).ToArray()),
            AppScreen.Settings => new("Settings", [$"Current window: {Window.Actual}",
                $"Current mode: {(Window.ActualFullscreen ? "Fullscreen" : "Windowed")}"],
            [new("volume", $"Master volume: {MathF.Round(EffectiveVolume * 100f):0}%", MenuAction.Volume, MenuControlKind.Volume),
             Button("mute", settings.Muted ? "Mute: ON" : "Mute: OFF", MenuAction.Mute),
             Button("music", $"Music: < {MusicTracks.Get(settings.MusicTrackId).DisplayName} >", MenuAction.Music),
             Button("display-mode", $"Display: < {(Window.SelectedFullscreen ? "Fullscreen" : "Windowed")} >", MenuAction.DisplayMode),
             Button("resolution", $"Resolution: < {Window.Selected} >", MenuAction.Resolution),
             Button("apply-resolution", "Apply resolution", MenuAction.ApplyResolution),
             Button("controls", "Controls", MenuAction.Controls), Back()]),
            AppScreen.SkinSelect => new("Choose a skin", ["Select a skin to apply immediately."],
                GameSkins.All.Select(skin => new MenuOption($"skin:{skin.Id}",
                    (skin.Id == settings.SkinId ? "[x] " : "[ ] ") + skin.DisplayName
                    + (skin.Id == settings.SkinId ? " (current)" : ""), MenuAction.ChooseSkin,
                    MenuControlKind.Skin, skin.Id, skin.SwatchColor,
                    Enabled: flow.Progress.IsSkinUnlocked(skin.Id), Detail: !flow.Progress.IsSkinUnlocked(skin.Id)
                        ? $"Locked: {skin.RequiredStars} stars" : skin.Id == settings.SkinId ? "Current" : "Unlocked",
                    Locked: !flow.Progress.IsSkinUnlocked(skin.Id)))
                .Concat([Back()]).ToArray()),
            AppScreen.Paused => new("Paused", ["Take a breath. Continue when ready."],
            [Button("continue", "Continue", MenuAction.Continue), Button("restart", "Restart", MenuAction.Restart),
             Button("settings", "Settings", MenuAction.Settings), Main()]),
            AppScreen.Failed => new("Try again", ["You fell or touched an obstacle.", "R: retry this level"],
            [Retry(), Button("levels", "Level select", MenuAction.Levels), Main()]),
            AppScreen.LevelResult => new("Level complete!", [CurrentCoins(), $"Rating: {Stars(PlayerProgress.Rating(flow.Snapshot!.CoinCount, catalog.Get(flow.Snapshot.LevelId).Coins.Count))}   Best: {Stars(flow.Progress.StarsFor(flow.Snapshot.LevelId))}"],
            [Button("next", "Next", MenuAction.Next), Retry(), Main()]),
            AppScreen.GameComplete => new(!catalog.Levels.Any(level => level.Id == GameLevels.FinalLevelId) || flow.Progress.StarsFor(GameLevels.FinalLevelId) > 0
                ? "Adventure complete!" : "Journey complete!", [$"Total coins: {flow.TotalCoinCount}", $"Adventure stars: {flow.Progress.TotalStars}/{flow.Progress.MaximumStars}",
                    flow.Progress.BaseStars < 12 ? "Earn 12 stars on the first four levels." : $"Sky Storm unlocked. Aim for {flow.Progress.MaximumStars}/{flow.Progress.MaximumStars} stars!"],
            [Button("levels", "Level select", MenuAction.Levels), Main()]),
            _ => new("", [], []),
        };
    }

    private static string Stars(int count) => "[" + new string('*', count) + new string('-', 3 - count) + "]";

    private string CurrentCoins()
    {
        var snapshot = flow.Snapshot!;
        return $"Coins {snapshot.CoinCount}/{catalog.Get(snapshot.LevelId).Coins.Count}";
    }
}
