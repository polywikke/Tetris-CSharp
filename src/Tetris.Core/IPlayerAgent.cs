namespace Tetris.Core;

/// <summary>
/// 自动玩家（C 实现，B 在人机/观战模式下调用）。每帧问一次，返回值会被 B 交给
/// <see cref="IGameEngine.Apply"/>。实现必须无副作用、不抛异常；推演只能基于
/// <see cref="GameState.Clone"/> 出来的副本。
/// </summary>
public interface IPlayerAgent
{
    /// <summary>显示在界面上的名字，例如"启发式 v1"。</summary>
    string Name { get; }

    /// <summary>开新局时调用，清空内部计划（"当前方块要落到第几列"之类）。</summary>
    void Reset();

    /// <summary>
    /// 每逻辑帧调用一次，返回这一帧想执行的动作。
    /// 允许返回 <see cref="GameCommand.None"/>（表示这一帧不动）。
    /// </summary>
    GameCommand NextCommand(GameState state);
}
