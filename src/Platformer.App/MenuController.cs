using System.Numerics;
using Platformer.Core;

namespace Platformer.App;

public readonly record struct MenuUpdateResult(bool ResetCamera);

/// <summary>
/// CPU-модель #19. Получает уже маршрутизированный InputFrame; окно/курсор/Step остаются у host #6.
/// Все переходы выполняет существующий LevelFlow. Callback громкости вызывается сразу и в первом меню.
/// </summary>
public sealed class MenuController
{
    private readonly LevelFlow flow;
    private readonly LevelCatalog catalog;
    private readonly Action<float> applyMasterVolume;
    private AppScreen observedScreen;
    private int selectedIndex;
    private string selectedLevelId;
    private string selectedSkinId;
    private Vector2? pointer;
    private Vector2? lastPointerPosition;
    private string? pressedId;
    private bool focused = true;

    public bool CloseRequested { get; private set; }
    public WindowSettings Window { get; } = new();

    public MenuController(LevelFlow flow, LevelCatalog catalog, Action<float>? applyMasterVolume = null)
    {
        ArgumentNullException.ThrowIfNull(flow);
        ArgumentNullException.ThrowIfNull(catalog);
        this.flow = flow;
        this.catalog = catalog;
        this.applyMasterVolume = applyMasterVolume ?? (_ => { });
        observedScreen = flow.Screen;
        selectedLevelId = catalog.Levels[0].Id;
        selectedSkinId = flow.UserSettings.SkinId;
        ApplyVolume();
    }

    public MenuView BuildView(float width, float height)
    {
        SynchronizeScreen();
        var snapshot = flow.Snapshot;
        HudModel? hud = flow.Screen == AppScreen.Playing && snapshot is not null
            ? HudModel.Create(catalog.Get(snapshot.LevelId), snapshot) : null;
        return MenuLayout.Build(flow.Screen, Page(), hud, flow.UserSettings.MasterVolume, selectedIndex,
            focused ? pointer : null, pressedId, width, height);
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
        if (input.RestartPressed && flow.Screen is AppScreen.Playing or AppScreen.Paused or AppScreen.Failed)
        {
            flow.Restart();
            SynchronizeScreen();
            return new(true);
        }
        if (flow.Screen == AppScreen.Playing) return default;
        var menu = input.Menu;
        if (menu.BackPressed)
        {
            flow.Back();
            SynchronizeScreen();
            return default;
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
            return new(resetCamera);
        }
        if (menu.HorizontalStep != 0 && option.Action == MenuAction.Volume)
            SetVolume(flow.UserSettings.MasterVolume + Math.Sign(menu.HorizontalStep) * 0.05f);
        if (menu.HorizontalStep != 0 && option.Action == MenuAction.Resolution)
            Window.Cycle(menu.HorizontalStep);
        return default;
    }

    private bool Execute(MenuOption option, Vector2? click, MenuRect bounds)
    {
        switch (option.Action)
        {
            case MenuAction.Resolution: Window.Cycle(1); break;
            case MenuAction.ApplyResolution: Window.RequestApply(); break;
            case MenuAction.Play: flow.Start(catalog.Levels[0].Id); return true;
            case MenuAction.Start: flow.Start(selectedLevelId); return true;
            case MenuAction.Restart: flow.Restart(); return true;
            case MenuAction.Next: flow.Next(); return true;
            case MenuAction.Continue: flow.Continue(); break;
            case MenuAction.Levels: flow.OpenLevelSelect(); break;
            case MenuAction.Settings: flow.OpenSettings(); break;
            case MenuAction.Skins: flow.OpenSkins(); break;
            case MenuAction.Back: flow.Back(); break;
            case MenuAction.MainMenu: flow.MainMenu(); break;
            case MenuAction.Exit: CloseRequested = true; break;
            case MenuAction.ChooseLevel: selectedLevelId = option.Value!; break;
            case MenuAction.ChooseSkin: selectedSkinId = option.Value!; break;
            case MenuAction.ApplySkin: flow.UserSettings.SkinId = selectedSkinId; break;
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
        flow.UserSettings.MasterVolume = Math.Clamp(MathF.Round(value * 20f) / 20f, 0f, 1f);
        ApplyVolume();
    }

    private void ApplyVolume() => applyMasterVolume(flow.UserSettings.Muted ? 0f : flow.UserSettings.MasterVolume);

    private void SynchronizeScreen()
    {
        if (flow.Screen == observedScreen) return;
        observedScreen = flow.Screen;
        selectedIndex = 0;
        pointer = null;
        pressedId = null;
        if (flow.Screen == AppScreen.SkinSelect) selectedSkinId = flow.UserSettings.SkinId;
        if (flow.Screen == AppScreen.Settings) Window.SelectActual();
    }

    private MenuPage Page()
    {
        MenuOption Button(string id, string label, MenuAction action) => new(id, label, action);
        MenuOption Back() => Button("back", "Back", MenuAction.Back);
        MenuOption Main() => Button("main-menu", "Main menu", MenuAction.MainMenu);
        MenuOption Retry() => Button("retry", "Retry", MenuAction.Restart);
        var settings = flow.UserSettings;
        return flow.Screen switch
        {
            AppScreen.MainMenu => new("Ball Adventure", ["Roll. Jump. Explore."],
            [Button("play", "Play", MenuAction.Play), Button("levels", "Levels", MenuAction.Levels),
             Button("settings", "Settings", MenuAction.Settings), Button("skins", "Skins", MenuAction.Skins),
             Button("exit", "Exit", MenuAction.Exit)]),
            AppScreen.LevelSelect => new("Choose a level", ["Choose a card, then Start."],
                catalog.Levels.Select(level => new MenuOption($"level:{level.Id}",
                    (level.Id == selectedLevelId ? "[x] " : "[ ] ") + HudModel.LevelNameFor(level.Id),
                    MenuAction.ChooseLevel, MenuControlKind.Level, level.Id))
                .Concat([Button("start", "Start", MenuAction.Start), Back()]).ToArray()),
            AppScreen.Settings => new("Settings", [$"Current window: {Window.Actual}", "Volume changes apply immediately."],
            [new("volume", $"Master volume: {MathF.Round(settings.MasterVolume * 100f):0}%", MenuAction.Volume, MenuControlKind.Volume),
             Button("mute", settings.Muted ? "Mute: ON" : "Mute: OFF", MenuAction.Mute),
             Button("resolution", $"Resolution: < {Window.Selected} >", MenuAction.Resolution),
             Button("apply-resolution", "Apply resolution", MenuAction.ApplyResolution), Back()]),
            AppScreen.SkinSelect => new("Choose a skin", ["Choose a sample, then Select to apply."],
                GameSkins.All.Select(skin => new MenuOption($"skin:{skin.Id}",
                    (skin.Id == selectedSkinId ? "[x] " : "[ ] ") + skin.DisplayName
                    + (skin.Id == settings.SkinId ? " (current)" : ""), MenuAction.ChooseSkin,
                    MenuControlKind.Skin, skin.Id, skin.Material.BaseColor))
                .Concat([Button("select", "Select", MenuAction.ApplySkin), Back()]).ToArray()),
            AppScreen.Paused => new("Paused", ["Take a breath. Continue when ready."],
            [Button("continue", "Continue", MenuAction.Continue), Button("restart", "Restart", MenuAction.Restart),
             Button("settings", "Settings", MenuAction.Settings), Main()]),
            AppScreen.Failed => new("Try again", ["You fell or touched an obstacle.", "R: retry this level"],
            [Retry(), Button("levels", "Level select", MenuAction.Levels), Main()]),
            AppScreen.LevelResult => new("Level complete!", [CurrentCoins()],
            [Button("next", "Next", MenuAction.Next), Retry(), Main()]),
            AppScreen.GameComplete => new("Adventure complete!", [$"Total coins: {flow.TotalCoinCount}"],
            [Button("levels", "Level select", MenuAction.Levels), Main()]),
            _ => new("", [], []),
        };
    }

    private string CurrentCoins()
    {
        var snapshot = flow.Snapshot!;
        return $"Coins {snapshot.CoinCount}/{catalog.Get(snapshot.LevelId).Coins.Count}";
    }
}
