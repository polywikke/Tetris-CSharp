namespace Tetris.Core;

/// <summary>一局游戏的生命周期。状态迁移由 <see cref="IGameEngine"/> 负责，界面只读。</summary>
public enum GameStatus
{
    /// <summary>尚未开始（界面可显示"按空格开始"）。</summary>
    Ready,

    /// <summary>进行中。</summary>
    Running,

    /// <summary>已暂停，重力不推进。</summary>
    Paused,

    /// <summary>已结束，不再接受操作。</summary>
    GameOver,
}
