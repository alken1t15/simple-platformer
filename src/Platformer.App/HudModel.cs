using System.Globalization;
using Platformer.Core;

namespace Platformer.App;

public enum BoostStatus { Ready, Active, Cooldown }

/// <summary>HUD читает принятый snapshot, а не считает монеты/время независимо от сеанса.</summary>
public sealed record HudModel(string LevelName, int CoinCount, int TotalCoins,
    BoostStatus Boost, float RemainingSeconds, bool ShowHelp, string? Hint = null)
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
            snapshot.SimulationSeconds < 8d, level.Id switch
            {
                "jump-circuit" => "Jump over striped walls. Touching one means retry.",
                "island-circuit" => "Follow the curves. Avoid the beam. Jump the final gap.",
                "coral-ascent" => "Climb the terraces. Jump up and balance on narrow paths.",
                "windmill-pass" => "Read the rotating beams. The outer edges offer a longer route.",
                "lava-causeway" => "Jump the volcanic barriers. The side balcony is optional.",
                "crumble-canyon" => "Keep moving on cracked tiles. Rest on the solid islands.",
                "temple-gauntlet" => "Balance, dodge, jump. Leave the final cracked tile quickly.",
                "pendulum-gorge" => "Wait for the stones to swing aside, then cross the narrow bridges.",
                "piston-foundry" => "Watch each pusher. Cross when it retreats. Keep moving on cracked tiles.",
                "clockwork-summit" => "Time the stone and pusher, then chain the final jumps.",
                "sky-storm" => "Climb the loop. Cracked tiles crumble after touch: keep moving!",
                _ => null,
            });
    }

    internal static string LevelNameFor(string id) => id switch
    {
        "level1" => "Training Track",
        "level2" => "Turning Maze",
        "jump-circuit" => "Jump Circuit",
        "island-circuit" => "Island Circuit",
        "coral-ascent" => "Coral Ascent",
        "windmill-pass" => "Windmill Pass",
        "lava-causeway" => "Lava Causeway",
        "crumble-canyon" => "Crumble Canyon",
        "temple-gauntlet" => "Temple Gauntlet",
        "pendulum-gorge" => "Pendulum Gorge",
        "piston-foundry" => "Piston Foundry",
        "clockwork-summit" => "Clockwork Summit",
        "sky-storm" => "Sky Storm",
        _ => new string(id.Select(character => character is >= ' ' and <= '~' ? character : '?').ToArray()),
    };
}
