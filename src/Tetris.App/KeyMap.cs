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

    /// <summary>
    /// 是否交给 <see cref="Input.KeyRepeater"/> 做 DAS/ARR 连发（方向键 + 软降）。
    /// 其余（旋转、硬降、暂停）都是<b>一次性</b>命令：按下当帧执行一次，按住不连发。
    /// </summary>
    public static bool IsAutoRepeat(Keys key) => key is Keys.Left or Keys.Right or Keys.Down;
}
