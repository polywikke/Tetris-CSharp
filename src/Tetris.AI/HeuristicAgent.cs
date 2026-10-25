using Tetris.Core;

namespace Tetris.AI;

/// <summary>
/// 启发式自动玩家骨架（C 负责实现）。当前实现只返回 <see cref="GameCommand.None"/>，
/// 也就是说接进界面后 AI 不会动——这是刻意的占位，方便先联调"人类/AI 切换"。
/// <para>实现要点见 docs/03-C-AI与测试.md §4：对每个 (旋转, 落点列) 做 Clone + 落底 + LockActive，
/// 再用高度、空洞、凹凸度、消行数打分，取最优解；每块只规划一次，然后逐帧下发移动指令。</para>
/// <para>同时实现 <see cref="IPlayerAgentDebug"/>，界面就能画出候选落点的热力图。</para>
/// </summary>
public sealed class HeuristicAgent : IPlayerAgent, IPlayerAgentDebug
{
    private static readonly IReadOnlyList<AgentCandidate> NoCandidates = Array.Empty<AgentCandidate>();

    private readonly GameConfig _config;
    private readonly IGameRules _rules;

    public HeuristicAgent(GameConfig config, IGameRules rules)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _rules = rules ?? throw new ArgumentNullException(nameof(rules));
    }

    public string Name => $"启发式(待实现, {_config.BoardWidth}x{_config.BoardHeight})";

    /// <summary>暴露给测试，确认规则依赖是从外部注入的。</summary>
    public IGameRules Rules => _rules;

    public IReadOnlyList<AgentCandidate> Candidates => NoCandidates;

    public AgentCandidate? Chosen => null;

    public void Reset()
    {
        // TODO(C)：清空当前方块的落点计划与候选缓存。
    }

    public GameCommand NextCommand(GameState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        // TODO(C)：实现启发式评估与搜索。下面的写法只是保持接口可用。
        return GameCommand.None;
    }
}
