namespace Platformer.App;

/// <summary>Размер клиентской области в логических пикселях, без рамки окна.</summary>
public readonly record struct WindowResolution(int Width, int Height)
{
    public override string ToString() => $"{Width} x {Height}";
}

/// <summary>CPU-модель выбора размера. Запрос не выдаётся за фактически применённый размер ОС.</summary>
public sealed class WindowSettings
{
    private static readonly WindowResolution[] Presets =
    [new(800, 450), new(960, 540), new(960, 640), new(1024, 576), new(1280, 720), new(1600, 900), new(1920, 1080)];
    private WindowResolution? requested;
    private (int, int, int, int, int, int)? lastState;
    public WindowResolution Actual { get; private set; } = new(960, 640);
    public WindowResolution Selected { get; private set; } = new(960, 640);
    public IReadOnlyList<WindowResolution> Choices { get; private set; } = Array.AsReadOnly(Presets);

    /// <summary>Host передаёт фактический размер и текущие границы из публичного API движка.
    /// Нулевые размеры свёрнутого окна не заменяют последний доступный размер.</summary>
    public void Synchronize(int width, int height, int minimumWidth, int minimumHeight, int maximumWidth, int maximumHeight)
    {
        if (minimumWidth <= 0 || minimumHeight <= 0 || maximumWidth < minimumWidth || maximumHeight < minimumHeight)
            throw new ArgumentOutOfRangeException(nameof(minimumWidth));
        var state = (width, height, minimumWidth, minimumHeight, maximumWidth, maximumHeight);
        if (lastState == state) return;
        lastState = state;
        if (width > 0 && height > 0) Actual = new(width, height);
        bool Fits(WindowResolution size) => size.Width >= minimumWidth && size.Width <= maximumWidth
            && size.Height >= minimumHeight && size.Height <= maximumHeight;
        var fallback = new WindowResolution(Math.Clamp(Actual.Width, minimumWidth, maximumWidth),
            Math.Clamp(Actual.Height, minimumHeight, maximumHeight));
        var choices = Presets.Append(fallback).Append(new(minimumWidth, minimumHeight))
            .Where(Fits).Distinct().OrderBy(size => size.Width).ThenBy(size => size.Height).ToArray();
        if (!Choices.SequenceEqual(choices)) Choices = Array.AsReadOnly(choices);
        if (!Choices.Contains(Selected)) Selected = fallback;
        if (requested is { } pending && !Fits(pending)) requested = null;
    }

    public void SelectActual() => Selected = Choices.MinBy(size =>
        Math.Abs((long)size.Width - Actual.Width) + Math.Abs((long)size.Height - Actual.Height));

    public void Cycle(int direction)
    {
        int index = Choices.ToList().IndexOf(Selected);
        Selected = Choices[(index + Math.Sign(direction) + Choices.Count) % Choices.Count];
    }

    public void RequestApply() => requested = Selected;

    /// <summary>Однократная команда для оконного adapter; подтверждение приходит через Synchronize.</summary>
    public WindowResolution? TakeRequest()
    {
        var result = requested;
        requested = null;
        return result;
    }
}
