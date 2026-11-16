using Engine3D.OpenGL;

namespace Platformer.App;

public enum GameAction { Forward, Left, Backward, Right, Jump, Boost, Restart }

/// <summary>Переназначение доступных публичному API движка клавиш; меню имеет независимые клавиши.</summary>
public sealed record ControlBindings(InputKey Forward = InputKey.W, InputKey Left = InputKey.A,
    InputKey Backward = InputKey.S, InputKey Right = InputKey.D, InputKey Jump = InputKey.Space,
    InputKey Boost = InputKey.LeftShift, InputKey Restart = InputKey.R)
{
    public static readonly InputKey[] Available = [InputKey.W, InputKey.A, InputKey.S, InputKey.D,
        InputKey.Up, InputKey.Left, InputKey.Down, InputKey.Right, InputKey.Space, InputKey.LeftShift, InputKey.RightShift, InputKey.R, InputKey.Enter];
    public InputKey For(GameAction action) => action switch
    {
        GameAction.Forward => Forward, GameAction.Left => Left, GameAction.Backward => Backward,
        GameAction.Right => Right, GameAction.Jump => Jump, GameAction.Boost => Boost, _ => Restart,
    };
    public ControlBindings Cycle(GameAction action, int direction)
    {
        if(direction==0)throw new ArgumentOutOfRangeException(nameof(direction));
        int index = Array.IndexOf(Available, For(action));
        InputKey next;
        do { index = (index + Math.Sign(direction) + Available.Length) % Available.Length; next = Available[index]; }
        while (Enum.GetValues<GameAction>().Any(a => a != action && For(a) == next));
        return action switch
        {
            GameAction.Forward => this with { Forward = next }, GameAction.Left => this with { Left = next },
            GameAction.Backward => this with { Backward = next }, GameAction.Right => this with { Right = next },
            GameAction.Jump => this with { Jump = next }, GameAction.Boost => this with { Boost = next },
            _ => this with { Restart = next },
        };
    }
    public bool IsValid => Enum.GetValues<GameAction>().All(a => Available.Contains(For(a)))
        && Enum.GetValues<GameAction>().Select(For).Distinct().Count() == 7;
}
