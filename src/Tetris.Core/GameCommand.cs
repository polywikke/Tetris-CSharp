namespace Tetris.Core;

/// <summary>
/// 玩家意图（不是"按键"）。B 把键盘映射成它，C 的 AI 也直接产出它，
/// A 的引擎消费它——三方因此互不认识对方的输入细节。
/// </summary>
public enum GameCommand
{
    /// <summary>什么都不做（AI 占位/无操作）。</summary>
    None,

    MoveLeft,
    MoveRight,

    /// <summary>主动下落一格。不计分。</summary>
    SoftDrop,

    /// <summary>直接落到最底并立即锁定。</summary>
    HardDrop,

    /// <summary>顺时针旋转。</summary>
    RotateCw,

    /// <summary>逆时针旋转。</summary>
    RotateCcw,

    /// <summary>暂停（仅 Running 时有效）。</summary>
    Pause,

    /// <summary>继续（仅 Paused 时有效）。</summary>
    Resume,
}
