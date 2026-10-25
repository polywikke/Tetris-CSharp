namespace Tetris.Core;

/// <summary>
/// 一局游戏的门面（A 实现，B 使用）。线程契约：<b>只在 UI 线程调用</b>，
/// 内部不得起线程、不得 async、不得弹窗、不得抛异常。
/// </summary>
public interface IGameEngine
{
    /// <summary>当前状态。界面随时可读；不要保存这个引用跨局使用。</summary>
    GameState State { get; }

    /// <summary>开新局：清空棋盘、重置分数/等级/消行，用给定种子重建方块序列。可重复调用。</summary>
    void Start(int seed);

    /// <summary>
    /// 执行一个意图。状态不允许时（如暂停中移动）返回 false，不改变任何状态、不抛异常。
    /// </summary>
    bool Apply(GameCommand command);

    /// <summary>
    /// 推进一帧逻辑，固定 1/60 秒。由界面按固定步长调用（卡顿时可连续补帧，但不要超过 3 帧）。
    /// 暂停或结束时为空操作。
    /// </summary>
    void Tick();
}
