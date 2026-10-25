namespace Tetris.Core;

/// <summary>
/// AI 的一次落点候选。给界面画"热力图 / 决策解释"用，不属于游戏逻辑。
/// </summary>
/// <param name="Kind">方块种类。</param>
/// <param name="Rotation">旋转态 0..3。</param>
/// <param name="X">落点：4×4 包围盒左上角列。</param>
/// <param name="Y">落点：4×4 包围盒左上角行（已落到底）。</param>
/// <param name="Score">评估总分，越大越好。</param>
/// <param name="ClearedLines">该落点能消的行数。</param>
/// <param name="Holes">落点后的空洞数。</param>
/// <param name="AggregateHeight">落点后各列高度之和。</param>
/// <param name="Bumpiness">落点后相邻列高度差之和。</param>
public sealed record AgentCandidate(
    PieceKind Kind,
    int Rotation,
    int X,
    int Y,
    double Score,
    int ClearedLines,
    int Holes,
    int AggregateHeight,
    int Bumpiness);

/// <summary>
/// 可选的调试接口：AI 除了实现 <see cref="IPlayerAgent"/>，再实现它，界面就能把
/// "它考虑过哪些落点、为什么选这个"画出来。不想做可视化可以完全不实现。
/// <para>约定：只读、无副作用；没有新决策时返回空列表 / null。</para>
/// </summary>
public interface IPlayerAgentDebug
{
    /// <summary>最近一次决策考虑过的全部候选（通常是 4 旋转 × 12 列里合法的那些）。</summary>
    IReadOnlyList<AgentCandidate> Candidates { get; }

    /// <summary>最近一次决策选中的候选；还没决策过则为 null。</summary>
    AgentCandidate? Chosen { get; }
}
