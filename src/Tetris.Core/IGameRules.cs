namespace Tetris.Core;

/// <summary>
/// 纯规则（A 实现，C 的 AI 复用）。约定：
/// <list type="bullet">
/// <item>所有方法<b>不抛异常</b>，非法操作返回 false / 0；</item>
/// <item>不依赖时间、随机数、界面；同一输入必然得到同一输出；</item>
/// <item>会修改传入的 <see cref="GameState"/>；C 推演前必须先 <see cref="GameState.Clone"/>。</item>
/// </list>
/// </summary>
public interface IGameRules
{
    /// <summary>方块按当前姿态放下是否会重叠或越界。</summary>
    bool Collides(GameState state, Tetromino piece);

    /// <summary>方块从当前位置竖直下落到不能再落为止，还能下落多少格（0 表示已经贴底）。</summary>
    int DropDistance(GameState state, Tetromino piece);

    /// <summary>左右移动一格；被挡住返回 false 且不改状态。</summary>
    bool TryMove(GameState state, int deltaX);

    /// <summary>旋转（+1 顺时针，-1 逆时针），含踢墙尝试；转不动返回 false 且不改状态。</summary>
    bool TryRotate(GameState state, int direction);

    /// <summary>
    /// 把 <see cref="GameState.Active"/> 固定进棋盘、消行、按 <see cref="GameConfig.ScoreRule"/> 加分、更新等级。
    /// 返回本次消除的行数。不会生成下一个方块（那是 <see cref="IGameEngine"/> 的事）。
    /// </summary>
    int LockActive(GameState state);
}
