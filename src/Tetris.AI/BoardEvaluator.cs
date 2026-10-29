using Tetris.Core;

namespace Tetris.AI;

/// <summary>
/// 一块棋盘的量化指标。四项特征的取值口径与 docs/设计说明.md §4.1 完全一致：
/// <list type="bullet">
/// <item><see cref="AggregateHeight"/>：各列高度之和，列高 = 该列最高格到棋盘顶部的距离；</item>
/// <item><see cref="Holes"/>：被固定方块压在下面的空格总数（每列单独数）；</item>
/// <item><see cref="Bumpiness"/>：相邻两列高度差的绝对值之和；</item>
/// <item><see cref="ClearedLines"/>：落下这一块消掉的行数。</item>
/// </list>
/// </summary>
/// <param name="AggregateHeight">各列高度之和。</param>
/// <param name="Holes">空洞数。</param>
/// <param name="Bumpiness">凹凸度。</param>
/// <param name="ClearedLines">本次消行数。</param>
public readonly record struct BoardMetrics(int AggregateHeight, int Holes, int Bumpiness, int ClearedLines);

/// <summary>
/// 评估函数（C 实现）。纯读操作：只观察 <see cref="GameState.Board"/>，不修改状态、不分配棋盘副本。
/// <para>
/// 评分 = 0.76 × 消行数 − 0.51 × 总高度 − 0.36 × 空洞数 − 0.18 × 凹凸度。<br/>
/// 权重为固定值（见 docs/设计说明.md §4.1）：消行是唯一正收益，其余三项都是惩罚项；
/// 空洞的惩罚仅次于消行，因为空洞只能靠消行补救，是局面变差的最强信号。
/// </para>
/// </summary>
public static class BoardEvaluator
{
    /// <summary>消一行的收益。</summary>
    public const double ClearedLinesWeight = 0.76;

    /// <summary>总高度的惩罚系数（负）。</summary>
    public const double AggregateHeightWeight = -0.51;

    /// <summary>空洞的惩罚系数（负）。</summary>
    public const double HolesWeight = -0.36;

    /// <summary>凹凸度的惩罚系数（负）。</summary>
    public const double BumpinessWeight = -0.18;

    /// <summary>按固定权重把四项指标折算成一个分数，越大越好。</summary>
    public static double Score(BoardMetrics metrics) =>
        (ClearedLinesWeight * metrics.ClearedLines)
        + (AggregateHeightWeight * metrics.AggregateHeight)
        + (HolesWeight * metrics.Holes)
        + (BumpinessWeight * metrics.Bumpiness);

    /// <summary>评估一个已经锁定完（消行也做完）的棋盘。</summary>
    /// <param name="state">棋盘状态；只读。</param>
    /// <param name="clearedLines">本次落子消掉的行数，默认 0。</param>
    public static double Evaluate(GameState state, int clearedLines = 0) =>
        Score(Measure(state, clearedLines));

    /// <summary>一次性算出四项指标。多次评估同一棋盘时可复用结果。</summary>
    public static BoardMetrics Measure(GameState state, int clearedLines = 0)
    {
        ArgumentNullException.ThrowIfNull(state);

        var heights = ColumnHeights(state);
        var aggregateHeight = 0;
        for (var x = 0; x < heights.Length; x++)
        {
            aggregateHeight += heights[x];
        }

        var bumpiness = 0;
        for (var x = 0; x < heights.Length - 1; x++)
        {
            bumpiness += Math.Abs(heights[x] - heights[x + 1]);
        }

        return new BoardMetrics(
            aggregateHeight,
            CountHoles(state),
            bumpiness,
            Math.Max(0, clearedLines));
    }

    /// <summary>每列高度：该列最上面一个已固定格到棋盘顶部的距离；空列为 0。</summary>
    public static int[] ColumnHeights(GameState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var heights = new int[state.Width];
        for (var x = 0; x < state.Width; x++)
        {
            for (var y = 0; y < state.Height; y++)
            {
                if (state.Board[x, y].HasValue)
                {
                    heights[x] = state.Height - y;
                    break;
                }
            }
        }

        return heights;
    }

    /// <summary>空洞数：同一列里，上方有方块、下方却为空的位置。</summary>
    public static int CountHoles(GameState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var holes = 0;
        for (var x = 0; x < state.Width; x++)
        {
            var blocked = false;
            for (var y = 0; y < state.Height; y++)
            {
                if (state.Board[x, y].HasValue)
                {
                    blocked = true;
                }
                else if (blocked)
                {
                    holes++;
                }
            }
        }

        return holes;
    }
}
