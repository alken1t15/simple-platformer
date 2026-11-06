using System.Globalization;
using Platformer.Core;

namespace Platformer.App;

public enum BoostStatus { Ready, Active, Cooldown }

/// <summary>HUD читает принятый snapshot, а не считает монеты/время независимо от сеанса.</summary>
public sealed record HudModel(string LevelName, int CoinCount, int TotalCoins,
    BoostStatus Boost, float RemainingSeconds, bool ShowHelp)
{
    public string BoostText => Boost switch
    {
        BoostStatus.Active => $"Boost active: {Seconds}s",
        BoostStatus.Cooldown => $"Boost cooldown: {Seconds}s",
        _ => "Boost ready",
    };

    private string Seconds => RemainingSeconds.ToString("0.0", CultureInfo.InvariantCulture);

    public static HudModel Create(LevelDefinition level, SessionSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(level);
        ArgumentNullException.ThrowIfNull(snapshot);
        if (level.Id != snapshot.LevelId)
            throw new ArgumentException("Snapshot должен принадлежать отображаемому уровню", nameof(snapshot));
        var ball = snapshot.Ball;
        BoostStatus status = ball.BoostRemainingSeconds > 0f ? BoostStatus.Active
            : ball.BoostCooldownSeconds > 0f ? BoostStatus.Cooldown : BoostStatus.Ready;
        float remaining = status == BoostStatus.Active ? ball.BoostRemainingSeconds : ball.BoostCooldownSeconds;
        return new(LevelNameFor(level.Id), snapshot.CoinCount, level.Coins.Count, status, remaining,
            snapshot.SimulationSeconds < 8d);
    }

    internal static string LevelNameFor(string id) => id switch
    {
        "level1" => "Training Track",
        "level2" => "Turning Maze",
        _ => new string(id.Select(character => character is >= ' ' and <= '~' ? character : '?').ToArray()),
    };
}
