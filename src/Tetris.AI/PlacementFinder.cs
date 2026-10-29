using Tetris.Core;

namespace Tetris.AI;

/// <summary>
/// 一次落点搜索的结果：<paramref name="Candidates"/> 是全部合法候选（按枚举顺序），
/// <paramref name="Best"/> 是评分最高的那个（棋盘放不下任何落点时为 null）。
/// </summary>
public sealed record PlacementSearchResult(
    IReadOnlyList<AgentCandidate> Candidates,
    AgentCandidate? Best);

/// <summary>
/// 落点搜索（C 实现）。只依赖注入的 <see cref="IGameRules"/>，不自己实现碰撞/消行，
/// 因此 A 的规则一交付就能直接用于实战，也方便测试里换成假规则。
/// <para>
/// <b>1-ply</b>：对 4 个旋转态 × 每一列，先 <see cref="GameState.Clone"/> 再试放、落到底、
/// <see cref="IGameRules.LockActive"/>，然后用 <see cref="BoardEvaluator"/> 打分。<br/>
/// <b>2-ply</b>（可选，默认<b>开启</b>）：先用 1-ply 分圈出"几乎并列最优"的一小撮候选
/// （差距不超过 <see cref="DefaultLookaheadTolerance"/>），只有在这一撮里才用
/// <c>当前分 + 0.4 × 下一块最优分</c> 做裁决。不参与裁决的候选仍用纯 1-ply 分，
/// 因此候选列表里的 <see cref="AgentCandidate.Score"/> 始终是同一把尺子。
/// </para>
/// <para>
/// 为什么不是"所有候选都加权"：那是第一版实现，实测 380 帧就顶死——加权后的分数
/// 普遍比未加权的低，AI 会为了"下一块能消一行"先把棋盘堆成塔。改成"只在并列最优里裁决"
/// 后，2-ply 只负责打破 1-ply 的犹豫，真引擎长跑的平均分高于纯 1-ply（见 docs/设计说明.md §6 的基准表）。
/// </para>
/// <para>整个过程<b>不修改传入的 state</b>：所有推演都在 Clone 上完成。</para>
/// </summary>
public sealed class PlacementFinder
{
    /// <summary>第二层的权重，默认 0.4（越看重未来越大，但不能盖过当前局面）。</summary>
    public const double DefaultLookaheadWeight = 0.4;

    /// <summary>
    /// "并列最优"的容差：只有 1-ply 分落在 <c>最优分 - 容差</c> 以内的候选，
    /// 才交给 2-ply 裁决。默认 0.15，约等于半行的价值。
    /// </summary>
    public const double DefaultLookaheadTolerance = 0.15;

    private readonly IGameRules _rules;
    private readonly bool _useLookahead;
    private readonly double _lookaheadWeight;
    private readonly double _lookaheadTolerance;

    /// <param name="rules">A 的规则实现（或测试替身）。</param>
    /// <param name="useLookahead">是否做 2-ply 裁决；关掉就退化成纯 1-ply。</param>
    /// <param name="lookaheadWeight">2-ply 第二层的权重。</param>
    /// <param name="lookaheadTolerance">"并列最优"的容差，见 <see cref="DefaultLookaheadTolerance"/>。</param>
    public PlacementFinder(
        IGameRules rules,
        bool useLookahead = true,
        double lookaheadWeight = DefaultLookaheadWeight,
        double lookaheadTolerance = DefaultLookaheadTolerance)
    {
        _rules = rules ?? throw new ArgumentNullException(nameof(rules));
        _useLookahead = useLookahead;
        _lookaheadWeight = lookaheadWeight;
        _lookaheadTolerance = Math.Max(0d, lookaheadTolerance);
    }

    /// <summary>是否启用了 2-ply 展望。</summary>
    public bool UsesLookahead => _useLookahead;

    /// <summary>当前方块的全部合法落点评分；不改动 <paramref name="state"/>。</summary>
    public PlacementSearchResult FindBest(GameState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var scored = Scan(state, state.Active);
        if (scored.Count == 0)
        {
            return new PlacementSearchResult(Array.Empty<AgentCandidate>(), null);
        }

        var candidates = new AgentCandidate[scored.Count];
        for (var i = 0; i < scored.Count; i++)
        {
            candidates[i] = scored[i].Candidate;
        }

        // 1) 纯 1-ply 的最优者。
        var chosen = BestIndex(candidates, chosen: 0, candidates.Length);

        // 2) 2-ply 只在"几乎并列最优"的小圈子里做裁决：
        //    加权分只用于裁决，不写回候选列表，保证所有 Score 仍是同一把尺子（纯 1-ply 分）。
        if (_useLookahead && state.NextQueue.Count > 0)
        {
            var nextKind = state.PeekNext(1)[0];
            var floor = candidates[chosen].Score - _lookaheadTolerance;
            var bestCombined = double.NegativeInfinity;

            for (var i = 0; i < scored.Count; i++)
            {
                if (candidates[i].Score < floor)
                {
                    continue;
                }

                var followUp = BestSinglePlyScore(scored[i].ResultingState, nextKind);
                if (!followUp.HasValue)
                {
                    continue;
                }

                var combined = candidates[i].Score + (_lookaheadWeight * followUp.Value);
                if (combined > bestCombined + ScoreEpsilon ||
                    (Math.Abs(combined - bestCombined) <= ScoreEpsilon && candidates[i].Score > candidates[chosen].Score))
                {
                    bestCombined = combined;
                    chosen = i;
                }
            }
        }

        return new PlacementSearchResult(candidates, candidates[chosen]);
    }

    /// <summary>浮点比较的容差。</summary>
    private const double ScoreEpsilon = 1e-9;

    private static int BestIndex(IReadOnlyList<AgentCandidate> candidates, int chosen, int count)
    {
        for (var i = 1; i < count; i++)
        {
            if (candidates[i].Score > candidates[chosen].Score)
            {
                chosen = i;
            }
        }

        return chosen;
    }

    /// <summary>
    /// 枚举 <paramref name="source"/> 的所有合法落点（不区分旋转态是否重复）。
    /// 供调试与测试使用；返回值中的 <see cref="AgentCandidate.Score"/> 是纯 1-ply 分。
    /// </summary>
    public IReadOnlyList<AgentCandidate> EnumeratePlacements(GameState state, Tetromino source)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(source);

        var scored = Scan(state, source);
        var result = new AgentCandidate[scored.Count];
        for (var i = 0; i < scored.Count; i++)
        {
            result[i] = scored[i].Candidate;
        }

        return result;
    }

    /// <summary>
    /// 1-ply 扫描：4 个旋转态 × 每一列，试放 + 落底 + 锁定 + 打分。
    /// 每次试放前都 Clone 一份棋盘，保证原状态不被污染。
    /// </summary>
    private List<ScoredPlacement> Scan(GameState state, Tetromino source)
    {
        var scored = new List<ScoredPlacement>(state.Width * 4);
        for (var rotation = 0; rotation < 4; rotation++)
        {
            var rotated = source.With(rotation: rotation);
            if (_rules.Collides(state, rotated))
            {
                // 这个旋转态在当前高度就放不下（例如贴顶），跳过。
                continue;
            }

            // 包围盒左列可以为负：越界与否由 Collides 按实际占用格判断。
            // 从 0 起会漏掉贴左墙的落点（例如竖放 I 永远进不了第 0、1 列）。
            for (var x = -(PieceShape.BoxSize - 1); x < state.Width; x++)
            {
                var placed = rotated.With(x: x);
                if (_rules.Collides(state, placed))
                {
                    continue;
                }

                var sim = state.Clone();
                var resting = placed.With(y: placed.Y + _rules.DropDistance(sim, placed));
                sim.Active = resting;
                var cleared = _rules.LockActive(sim);

                var metrics = BoardEvaluator.Measure(sim, cleared);
                var candidate = new AgentCandidate(
                    resting.Kind,
                    rotation,
                    x,
                    resting.Y,
                    BoardEvaluator.Score(metrics),
                    cleared,
                    metrics.Holes,
                    metrics.AggregateHeight,
                    metrics.Bumpiness);

                scored.Add(new ScoredPlacement(candidate, sim));
            }
        }

        return scored;
    }

    /// <summary>下一块方块放在 <paramref name="boardState"/> 上能拿到的最高 1-ply 分。</summary>
    private double? BestSinglePlyScore(GameState boardState, PieceKind kind)
    {
        var spawned = new Tetromino(kind, boardState.ShapeOf(kind), boardState.SpawnX(), 0);
        if (_rules.Collides(boardState, spawned))
        {
            return null;
        }

        var scored = Scan(boardState, spawned);
        if (scored.Count == 0)
        {
            return null;
        }

        var best = double.NegativeInfinity;
        foreach (var placement in scored)
        {
            if (placement.Candidate.Score > best)
            {
                best = placement.Candidate.Score;
            }
        }

        return best;
    }

    /// <summary>候选 + 它推演出来的棋盘（只用于 2-ply，不对外暴露）。</summary>
    private readonly record struct ScoredPlacement(AgentCandidate Candidate, GameState ResultingState);
}
