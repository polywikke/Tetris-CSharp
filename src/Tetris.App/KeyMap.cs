using Tetris.Core;

namespace Tetris.App;

/// <summary>键盘 → 意图的映射（B 负责）。界面之外的所有逻辑都只认识 <see cref="GameCommand"/>。</summary>
internal static class KeyMap
{
    public static GameCommand? ToCommand(Keys key) => key switch
    {
        Keys.Left => GameCommand.MoveLeft,
        Keys.Right => GameCommand.MoveRight,
        Keys.Down => GameCommand.SoftDrop,
        Keys.Up => GameCommand.RotateCw,
        Keys.X => GameCommand.RotateCw,
        Keys.Z => GameCommand.RotateCcw,
        Keys.Space => GameCommand.HardDrop,
        Keys.P => GameCommand.Pause,
        Keys.Escape => GameCommand.Pause,
        Keys.Enter => GameCommand.Resume,
        _ => null,
    };
}
