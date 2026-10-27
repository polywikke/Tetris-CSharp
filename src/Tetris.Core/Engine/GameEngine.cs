namespace Tetris.Core.Engine;

/// <summary>
/// 真实引擎（A 负责）：把 <see cref="IGameRules"/> 的纯规则串成"开局 → 下落 → 锁定消行 → 出新块"的循环，
/// 并负责 7-bag 序列、预览队列、暂停与结束判定。
/// <para>
/// 线程契约与 <see cref="IGameEngine"/> 一致：只在 UI 线程调用，内部不起线程、不 async、不弹窗。
/// 界面每帧读 <see cref="State"/>，<b>不要缓存这个引用跨局使用</b>——<see cref="Start"/> 会换一个新对象。
/// </para>
/// <para>
/// 交付进度：开局、7-bag 与预览队列已完成；重力推进（<see cref="Tick"/>）与命令处理
/// （<see cref="Apply"/>）按任务书顺序后续补在本类中，在补齐之前它们会抛
/// <see cref="NotImplementedException"/>，避免静默假装成功。
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

        SpawnNext();
    }

    /// <summary>TODO(④)：命令处理（移动/旋转/软降/硬降/暂停/继续），尚未实现。</summary>
    /// <exception cref="NotImplementedException">命令处理补齐之前调用会抛出。</exception>
    public bool Apply(GameCommand command) =>
        throw new NotImplementedException("TODO(④)：命令处理尚未实现（A 的交付 ④）。");

    /// <summary>TODO(④)：按重力推进一帧，尚未实现。</summary>
    /// <exception cref="NotImplementedException">重力推进补齐之前调用会抛出。</exception>
    public void Tick() =>
        throw new NotImplementedException("TODO(④)：重力推进尚未实现（A 的交付 ④）。");

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
