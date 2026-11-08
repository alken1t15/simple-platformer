namespace Platformer.App;

/// <summary>Размер клиентской области в логических пикселях, без рамки окна.</summary>
public readonly record struct WindowResolution(int Width, int Height)
{
    public override string ToString() => $"{Width} x {Height}";
}

public readonly record struct WindowDisplayRequest(WindowResolution Resolution, bool Fullscreen);

/// <summary>CPU-модель выбора размера. Запрос не выдаётся за фактически применённый размер ОС.</summary>
public sealed class WindowSettings
{
    private static readonly WindowResolution[] Presets =
    [new(800, 450), new(960, 540), new(960, 640), new(1024, 576), new(1280, 720), new(1600, 900), new(1920, 1080)];
    private WindowDisplayRequest? requested;
    private (int, int, int, int, int, int, bool, int, int)? lastState;
    private WindowResolution windowedSelection = new(960, 640);
    private WindowResolution windowedActual = new(960, 640);
    private WindowResolution fullscreenSize;
    private int minWidth = 800, minHeight = 450, maxWidth = int.MaxValue, maxHeight = int.MaxValue;
    public WindowResolution Actual { get; private set; } = new(960, 640);
    public WindowResolution Selected { get; private set; } = new(960, 640);
    public bool ActualFullscreen { get; private set; }
    public bool SelectedFullscreen { get; private set; }
    public IReadOnlyList<WindowResolution> Choices { get; private set; } = Array.AsReadOnly(Presets);

    /// <summary>Host передаёт фактический размер и текущие границы из публичного API движка.
    /// Нулевые размеры свёрнутого окна не заменяют последний доступный размер.</summary>
    public void Synchronize(int width, int height, int minimumWidth, int minimumHeight, int maximumWidth, int maximumHeight,
        bool fullscreen = false, int fullscreenWidth = 0, int fullscreenHeight = 0)
    {
        if (minimumWidth <= 0 || minimumHeight <= 0 || maximumWidth < minimumWidth || maximumHeight < minimumHeight)
            throw new ArgumentOutOfRangeException(nameof(minimumWidth));
        var state = (width, height, minimumWidth, minimumHeight, maximumWidth, maximumHeight,
            fullscreen, fullscreenWidth, fullscreenHeight);
        if (lastState == state) return;
        lastState = state;
        if (width > 0 && height > 0) Actual = new(width, height);
        ActualFullscreen = fullscreen;
        if (!fullscreen && width > 0 && height > 0) windowedActual = Actual;
        fullscreenSize = new(fullscreenWidth, fullscreenHeight);
        minWidth = minimumWidth; minHeight = minimumHeight; maxWidth = maximumWidth; maxHeight = maximumHeight;
        if (SelectedFullscreen && !HasFullscreen) SelectedFullscreen = false;
        RebuildChoices();
        if (requested is { } pending && (pending.Fullscreen ? !HasFullscreen || pending.Resolution != fullscreenSize
            : !FitsWindow(pending.Resolution))) requested = null;
    }

    private bool HasFullscreen => fullscreenSize.Width > 0 && fullscreenSize.Height > 0;
    private bool FitsWindow(WindowResolution size) => size.Width >= minWidth && size.Width <= maxWidth
        && size.Height >= minHeight && size.Height <= maxHeight;

    private void RebuildChoices()
    {
        if (SelectedFullscreen)
        {
            Selected = fullscreenSize;
            Choices = Array.AsReadOnly(new[] { fullscreenSize });
            return;
        }
        var fallback = new WindowResolution(Math.Clamp(windowedActual.Width, minWidth, maxWidth),
            Math.Clamp(windowedActual.Height, minHeight, maxHeight));
        var choices = Presets.Append(fallback).Append(new(minWidth, minHeight))
            .Where(FitsWindow).Distinct().OrderBy(size => size.Width).ThenBy(size => size.Height).ToArray();
        if (!Choices.SequenceEqual(choices)) Choices = Array.AsReadOnly(choices);
        if (!Choices.Contains(Selected)) Selected = fallback;
    }

    public void SelectActual()
    {
        SelectedFullscreen = ActualFullscreen && HasFullscreen;
        Selected = Actual;
        RebuildChoices();
        if (!SelectedFullscreen) windowedSelection = Selected;
    }

    public void ToggleMode()
    {
        if (!HasFullscreen) return;
        if (!SelectedFullscreen) windowedSelection = Selected;
        SelectedFullscreen = !SelectedFullscreen;
        Selected = SelectedFullscreen ? fullscreenSize : windowedSelection;
        RebuildChoices();
    }

    public void Cycle(int direction)
    {
        int index = Choices.ToList().IndexOf(Selected);
        Selected = Choices[(index + Math.Sign(direction) + Choices.Count) % Choices.Count];
    }

    public void RequestApply() => requested = new(Selected, SelectedFullscreen);

    /// <summary>Однократная команда для оконного adapter; подтверждение приходит через Synchronize.</summary>
    public WindowResolution? TakeRequest()
        => TakeDisplayRequest()?.Resolution;

    /// <summary>Однократный запрос режима и размера для единственного оконного adapter;
    /// повторное чтение возвращает null до следующего RequestApply.</summary>
    public WindowDisplayRequest? TakeDisplayRequest()
    {
        var result = requested;
        requested = null;
        return result;
    }
}
