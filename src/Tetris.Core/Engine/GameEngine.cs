namespace Tetris.Core.Engine;

/// <summary>
/// 真实引擎（A 负责）：把 <see cref="IGameRules"/> 的纯规则串成"开局 → 下落 → 锁定消行 → 出新块"的循环，
/// 并负责 7-bag 序列、预览队列、暂停与结束判定。
/// <para>
/// 线程契约与 <see cref="IGameEngine"/> 一致：只在 UI 线程调用，内部不起线程、不 async、不弹窗。
/// 界面每帧读 <see cref="State"/>，<b>不要缓存这个引用跨局使用</b>——<see cref="Start"/> 会换一个新对象。
/// </para>
/// <para>
/// 命令处理遵循任务书 5.7：<see cref="GameStatus.Running"/> 之外的状态，除
/// <see cref="GameCommand.Resume"/> 外一律拒绝且不改状态；软降贴底只返回 <c>false</c>，不会锁定。
/// </para>
/// </summary>
public sealed class GameEngine : IGameEngine
{
    /// <summary>逻辑帧固定 1/60 秒；重力按毫秒累加，所以任何等级都用整数帧推进。</summary>
    private const double FrameMs = 1000.0 / 60.0;

    private readonly GameConfig _config;
    private readonly IGameRules _rules;
    private readonly IRandomSource _random;
    private readonly SevenBag _bag;

    private GameState _state;

    /// <summary>重力计时器：每帧累加 1/60 秒，攒到 <see cref="GameConfig.GravityMsFor"/> 就下落一格。</summary>
    private double _accumulatorMs;

    public GameEngine(GameConfig config, IGameRules rules, IRandomSource random)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _rules = rules ?? throw new ArgumentNullException(nameof(rules));
        _random = random ?? throw new ArgumentNullException(nameof(random));

        _bag = new SevenBag(_random);
        _state = new GameState(_config);
    }

    /// <summary>当前状态。开局后是一个新对象，界面每帧重新取。</summary>
    public GameState State => _state;

    /// <summary>
    /// 开新局：重置随机源 → 清 bag → 换一份干净状态 → 出第一个方块。
    /// 生成后若立刻碰撞（棋盘已堆满），状态直接变成 <see cref="GameStatus.GameOver"/>。
    /// 可重复调用，每次都会丢弃上一局。
    /// </summary>
    public void Start(int seed)
    {
        _random.Reseed(seed);
        _bag.Clear();

        _state = new GameState(_config) { Status = GameStatus.Running };
        _accumulatorMs = 0;

        SpawnNext();
    }

    /// <summary>
    /// 执行一个意图。只有进行中才接受移动/旋转/软降/硬降/暂停；暂停中只接受
    /// <see cref="GameCommand.Resume"/>。被拒绝时返回 <c>false</c>、不改任何状态、不抛异常。
    /// </summary>
    public bool Apply(GameCommand command)
    {
        // Resume 是唯一在非 Running 状态下合法的命令，必须先于状态门禁处理。
        if (command == GameCommand.Resume)
        {
            if (_state.Status != GameStatus.Paused)
            {
                return false;
            }

            _state.Status = GameStatus.Running;
            return true;
        }

        if (_state.Status != GameStatus.Running)
        {
            return false;
        }

        switch (command)
        {
            case GameCommand.MoveLeft:
                return _rules.TryMove(_state, -1);

            case GameCommand.MoveRight:
                return _rules.TryMove(_state, 1);

            case GameCommand.RotateCw:
                return _rules.TryRotate(_state, 1);

            case GameCommand.RotateCcw:
                return _rules.TryRotate(_state, -1);

            case GameCommand.SoftDrop:
                return SoftDrop();

            case GameCommand.HardDrop:
                return HardDrop();

            case GameCommand.Pause:
                _state.Status = GameStatus.Paused;
                return true;

            // GameCommand.None 以及将来新增但这里还没接的命令：一律当作无效意图。
            default:
                return false;
        }
    }

    /// <summary>软降一格：能下就下并返回 true；贴底返回 false，且<b>不锁定</b>（锁定交给重力或硬降）。</summary>
    private bool SoftDrop()
    {
        var dropped = _state.Active.With(y: _state.Active.Y + 1);
        if (_rules.Collides(_state, dropped))
        {
            return false;
        }

        _state.Active = dropped;
        return true;
    }

    /// <summary>硬降：直接落到底并立即锁定出新块。距离为 0 时也照常锁定，不会"卡住不落"。</summary>
    private bool HardDrop()
    {
        var distance = _rules.DropDistance(_state, _state.Active);
        if (distance > 0)
        {
            _state.Active = _state.Active.With(y: _state.Active.Y + distance);
        }

        LockAndSpawn();
        return true;
    }

    /// <summary>
    /// 推进一帧：每帧累加 1/60 秒，攒够 <see cref="GameConfig.GravityMsFor"/> 就下落一格；
    /// 已经贴底则锁定并出新块。非 <see cref="GameStatus.Running"/> 状态是空操作
    /// （暂停时 <see cref="GameState.Frame"/> 不增长）。
    /// </summary>
    public void Tick()
    {
        if (_state.Status != GameStatus.Running)
        {
            return;
        }

        _state.Frame++;
        _accumulatorMs += FrameMs;

        if (_accumulatorMs < _config.GravityMsFor(_state.Level))
        {
            return;
        }

        // 按任务书 5.5：计时器直接清零，而不是减去重力间隔。
        _accumulatorMs = 0;

        var fallen = _state.Active.With(y: _state.Active.Y + 1);
        if (!_rules.Collides(_state, fallen))
        {
            _state.Active = fallen;
            return;
        }

        LockAndSpawn();
    }

    /// <summary>
    /// 贴底后：交给规则锁定（写盘、消行、计分、升级），再出下一个方块。
    /// 累加器清零，新块从完整的一个重力间隔重新计时。
    /// </summary>
    private void LockAndSpawn()
    {
        _rules.LockActive(_state);
        SpawnNext();
        _accumulatorMs = 0;
    }

    /// <summary>
    /// 出下一个方块：先把预览队列补到 <c>PreviewCount + 1</c>，再出队一个放到生成位置；
    /// 若生成姿态已经碰撞，说明堆到顶了，直接判负。
    /// </summary>
    private void SpawnNext()
    {
        RefillPreviewQueue();

        var kind = _state.NextQueue.Dequeue();
        var piece = new Tetromino(kind, _state.ShapeOf(kind), _state.SpawnX(), 0);
        _state.Active = piece;

        if (_rules.Collides(_state, piece))
        {
            _state.Status = GameStatus.GameOver;
        }
    }

    /// <summary>
    /// 把预览队列补到 <c>PreviewCount + 1</c> 个：多出的那一个保证界面能显示
    /// "当前块 + 接下来 PreviewCount 个"，同时出队后队列不会见底。
    /// </summary>
    private void RefillPreviewQueue()
    {
        var target = Math.Max(1, _config.PreviewCount + 1);
        while (_state.NextQueue.Count < target)
        {
            _state.NextQueue.Enqueue(_bag.Next());
        }
    }
}
