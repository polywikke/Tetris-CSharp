namespace Tetris.Core;

/// <summary>
/// 计分规则。默认实现 <see cref="StandardScoreRule"/> 满足需求"一次消除 k 行得 100 × k 分"。
/// 换规则 = 换一个实现并在 GameConfig 里传进去，核心代码不动。
/// </summary>
public interface IScoreRule
{
    /// <param name="linesCleared">本次消除的行数，0 表示没消行。</param>
    /// <param name="level">当前等级（可用来做等级加成）。</param>
    int ScoreFor(int linesCleared, int level);
}
