using Tetris.Core;

namespace Tetris.AI;

/// <summary>
/// 启发式自动玩家（C 实现）：用 <see cref="PlacementFinder"/> 为当前方块选一个落点，
/// 再用一个每帧只发一条指令的状态机把方块挪过去，最后硬降锁定。
/// <para>
/// 指令状态机（docs/设计说明.md §4.3）：
/// <list type="number">
/// <item>没有计划、或换了一块新方块 → 重新搜索落点；</item>
/// <item>当前旋转态不是计划里的 → <see cref="GameCommand.RotateCw"/>；</item>
/// <item>列还不对 → <see cref="GameCommand.MoveLeft"/> / <see cref="GameCommand.MoveRight"/>；</item>
/// <item>都对了 → <see cref="GameCommand.HardDrop"/>，并清空计划等下一块。</item>
/// </list>
/// 三条兜底：旋转连续 4 帧没转到就放弃旋转直接降；同一块超过 120 帧强制硬降；
/// 棋盘上没有任何合法落点（或连续两帧状态完全没变化）时也直接硬降，
/// 保证 <see cref="NextCommand"/> <b>永远不抛异常、永远有返回值</b>。
/// </para>
/// <para>
/// 同时实现 <see cref="IPlayerAgentDebug"/>：界面可以用 <see cref="Candidates"/> 画候选热力图、
/// 用 <see cref="Chosen"/> 高亮 AI 选中的落点。
/// </para>
/// </summary>
public sealed class HeuristicAgent : IPlayerAgent, IPlayerAgentDebug
{
    /// <summary>同一块方块的存活上限（帧）。超过就强制硬降，防止卡死。</summary>
    public const int MaxFramesPerPiece = 120;

    /// <summary>旋转最多尝试的帧数，超过就放弃旋转。</summary>
    public const int MaxRotationAttempts = 4;

    /// <summary>连续多少帧"方块位置完全没变"就判定被卡住，重新规划。</summary>
    public const int StuckFrameThreshold = 2;

    /// <summary>卡住后最多重规划几次；超过说明落点本身不可达，直接硬降。</summary>
    public const int MaxStuckReplans = 3;

    private static readonly IReadOnlyList<AgentCandidate> NoCandidates = Array.Empty<AgentCandidate>();

    private readonly GameConfig _config;
    private readonly IGameRules _rules;
    private readonly PlacementFinder _finder;

    private IReadOnlyList<AgentCandidate> _candidates = NoCandidates;
    private AgentCandidate? _chosen;
    private AgentCandidate? _plan;
    private int _framesOnPiece;
    private int _rotationAttempts;
    private int _stuckFrames;
    private int _stuckReplans;
    private bool _rotationAbandoned;

    private int _lastX = int.MinValue;
    private int _lastY = int.MinValue;
    private int _lastRotation = int.MinValue;

    /// <param name="config">棋盘尺寸等参数（与引擎共用同一个实例）。</param>
    /// <param name="rules">A 的规则实现。</param>
    /// <param name="useLookahead">是否用 2-ply 裁决"并列最优"；关掉就是纯 1-ply（更快，也更短视）。</param>
    /// <param name="lookaheadWeight">2-ply 第二层的权重。</param>
    /// <param name="lookaheadTolerance">"并列最优"的容差，见 <see cref="PlacementFinder.DefaultLookaheadTolerance"/>。</param>
    public HeuristicAgent(
        GameConfig config,
        IGameRules rules,
        bool useLookahead = true,
        double lookaheadWeight = PlacementFinder.DefaultLookaheadWeight,
        double lookaheadTolerance = PlacementFinder.DefaultLookaheadTolerance)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _rules = rules ?? throw new ArgumentNullException(nameof(rules));
        _finder = new PlacementFinder(_rules, useLookahead, lookaheadWeight, lookaheadTolerance);
    }

    public string Name => $"启发式 v1({_config.BoardWidth}x{_config.BoardHeight})";

    /// <summary>暴露给测试，确认规则依赖是从外部注入的。</summary>
    public IGameRules Rules => _rules;

    /// <summary>最近一次决策考虑过的全部候选；还没决策过时为空列表（不为 null）。</summary>
    public IReadOnlyList<AgentCandidate> Candidates => _candidates;

    /// <summary>最近一次决策选中的候选；还没决策过时为 null。</summary>
    public AgentCandidate? Chosen => _chosen;

    /// <summary>当前正在执行的落点计划（测试用）。</summary>
    public AgentCandidate? Plan => _plan;

    /// <summary>清空落点计划与决策缓存，回到"还没决定过"的状态。</summary>
    public void Reset()
    {
        _candidates = NoCandidates;
        _chosen = null;
        _plan = null;
        _framesOnPiece = 0;
        _rotationAttempts = 0;
        _stuckFrames = 0;
        _stuckReplans = 0;
        _rotationAbandoned = false;
        _lastX = int.MinValue;
        _lastY = int.MinValue;
        _lastRotation = int.MinValue;
    }

    public GameCommand NextCommand(GameState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (state.Status == GameStatus.GameOver)
        {
            Reset();
            return GameCommand.None;
        }

        if (state.Status != GameStatus.Running)
        {
            // Ready / Paused：不开新计划，等引擎把状态切回 Running。
            return GameCommand.None;
        }

        if (_plan is null || _plan.Kind != state.Active.Kind)
        {
            _framesOnPiece = 0;
            _stuckReplans = 0;
            Replan(state);
            if (_plan is null)
            {
                // 棋盘上没有任何合法落点：直接砸下去，让引擎自己判 GameOver。
                return HardDropAndForget();
            }
        }

        _framesOnPiece++;
        if (_framesOnPiece > MaxFramesPerPiece)
        {
            return HardDropAndForget();
        }

        if (IsStuck(state))
        {
            // 连续重规划仍然是同一个落点、又挪不动 → 说明计划本身不可达（被墙/被堆顶卡住），
            // 再算下去只会死循环，直接硬降把方块处理掉。
            _stuckReplans++;
            if (_stuckReplans > MaxStuckReplans)
            {
                return HardDropAndForget();
            }

            Replan(state);
            if (_plan is null)
            {
                return HardDropAndForget();
            }
        }

        var plan = _plan!;

        if (!_rotationAbandoned && state.Active.Rotation != plan.Rotation)
        {
            if (_rotationAttempts < MaxRotationAttempts)
            {
                _rotationAttempts++;
                return GameCommand.RotateCw;
            }

            // 转了 4 帧还没到位（多半被墙/被堆顶卡住）：放弃旋转，直接降。
            _rotationAbandoned = true;
        }

        if (state.Active.X < plan.X)
        {
            return GameCommand.MoveRight;
        }

        if (state.Active.X > plan.X)
        {
            return GameCommand.MoveLeft;
        }

        return HardDropAndForget();
    }

    /// <summary>重新搜索落点；搜不到时保留空计划。不修改传入的 state。</summary>
    private void Replan(GameState state)
    {
        var search = _finder.FindBest(state);
        _candidates = search.Candidates;
        _chosen = search.Best;
        _plan = search.Best;
        _rotationAttempts = 0;
        _stuckFrames = 0;
        _rotationAbandoned = false;
        _lastX = int.MinValue;
        _lastY = int.MinValue;
        _lastRotation = int.MinValue;
    }

    /// <summary>方块位置/朝向连续若干帧没变化 → 认为指令被挡住，需要重新规划。</summary>
    private bool IsStuck(GameState state)
    {
        var unchanged = state.Active.X == _lastX
            && state.Active.Y == _lastY
            && state.Active.Rotation == _lastRotation;

        _lastX = state.Active.X;
        _lastY = state.Active.Y;
        _lastRotation = state.Active.Rotation;

        _stuckFrames = unchanged ? _stuckFrames + 1 : 0;
        return _stuckFrames >= StuckFrameThreshold;
    }

    /// <summary>下发硬降并清空计划：下一块方块会重新规划。</summary>
    private GameCommand HardDropAndForget()
    {
        _plan = null;
        _framesOnPiece = 0;
        _rotationAttempts = 0;
        _stuckFrames = 0;
        _rotationAbandoned = false;
        _lastX = int.MinValue;
        _lastY = int.MinValue;
        _lastRotation = int.MinValue;
        return GameCommand.HardDrop;
    }
}
