namespace Platformer.Core;

/// <summary>Таймер начинает отсчёт после первого контакта; состояние публикуется вместе с tick.</summary>
public readonly record struct CrumblingPlatformState(string Id, double? TriggeredAtSeconds, bool Collapsed);
