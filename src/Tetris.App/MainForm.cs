using System.Diagnostics;
using System.Drawing;
using Tetris.AI;
using Tetris.App.Input;
using Tetris.Core;

namespace Tetris.App;

/// <summary>
/// 主窗口（B 负责）：负责"定时器驱动逻辑帧 + 把状态画出来 + 把按键翻译成命令"。
/// 它不认识 A 的规则实现，只认识 <see cref="IGameEngine"/> / <see cref="IGameRules"/> 两个接口；
/// AI 也是通过 <see cref="IPlayerAgent"/> 接入的，界面不知道 C 的启发式是怎么算的。
/// <para>
/// 输入分两路：
/// <list type="bullet">
/// <item><b>一次性</b>命令（旋转、硬降、暂停）在 <see cref="ProcessCmdKey"/> 里按下当帧直接下发，
/// 按住不放不会连发；</item>
/// <item><b>可重复</b>命令（左右移动、软降）只登记到 <see cref="KeyRepeater"/>，由它在每个逻辑帧
/// 之前按 DAS 180ms / ARR 40ms 决定发不发、发几条。</item>
/// </list>
/// </para>
/// <para>
/// F3 是"人机轮流"开关：同一个棋盘、同一个引擎实例，打开时 AI 每帧贡献一条命令，
/// 关闭时玩家立刻接手当前方块（AI 期间玩家的移动/旋转/硬降被忽略，但暂停仍然有效）。
/// </para>
/// </summary>
public sealed class MainForm : Form
{
    /// <summary>逻辑帧固定 1/60 秒，与渲染帧率解耦。</summary>
    private const double FrameMs = 1000.0 / 60.0;

    /// <summary>一次刷新最多补 3 帧，防止长时间卡顿后疯狂追帧导致假死。</summary>
    private const int MaxCatchUpFrames = 3;

    private readonly IGameEngine _engine = AppBootstrap.CreateEngine(Environment.TickCount);
    private readonly IHighScoreStore _store = AppBootstrap.CreateStore();
    private readonly BoardView _board = new();
    private readonly KeyRepeater _keys = new();
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 15 };
    private readonly Stopwatch _clock = Stopwatch.StartNew();

    private Label _scoreLabel = null!;
    private Label _highLabel = null!;
    private Label _levelLabel = null!;
    private Label _linesLabel = null!;
    private Label _nextLabel = null!;
    private Label _hintLabel = null!;
    private Label _aiLabel = null!;
    private Label _noticeLabel = null!;
    private NextPieceView[] _previews = Array.Empty<NextPieceView>();

    private HighScore? _highScore;
    private TimeSpan _lastTick;
    private double _pendingMs;
    private bool _savedThisRound;
    private bool _newRecordThisRound;
    private IPlayerAgent? _agent;

    public MainForm()
    {
        Text = "俄罗斯方块 · C# 大作业";
        StartPosition = FormStartPosition.CenterScreen;
        Size = new Size(860, 660);
        MinimumSize = new Size(720, 560);
        BackColor = Palette.Background;
        KeyPreview = true;
        AutoScaleMode = AutoScaleMode.Dpi;

        _board.State = _engine.State;
        Controls.Add(BuildLayout());

        Load += OnFormLoad;
        FormClosing += (_, _) => _timer.Stop();
        _timer.Tick += OnFrame;
        KeyUp += (_, e) => _keys.Release(e.KeyCode);
    }

    /// <summary>
    /// 键盘入口。注意 <c>keyData</c> 可能带 Ctrl/Shift 之类的修饰位，
    /// 这里先裁成纯键码，保证按下与松开用的是同一个 key（否则"按住不连发"会失效）。
    /// </summary>
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        var key = keyData & Keys.KeyCode;

        if (key == Keys.F2)
        {
            StartNewGame();
            return true;
        }

        if (key == Keys.F3)
        {
            ToggleAgent();
            return true;
        }

        var command = KeyMap.ToCommand(key);
        if (command is null)
        {
            return base.ProcessCmdKey(ref msg, keyData);
        }

        // 同一个键的第二条 KeyDown 就是 Windows 的自动重复：吞掉，别让它一按就刷出一串命令。
        if (!_keys.Press(key, NowMs()))
        {
            return true;
        }

        if (KeyMap.IsAutoRepeat(key))
        {
            // 方向键 / 软降不在这里执行，交给 OnFrame 的 KeyRepeater 按 DAS/ARR 节奏发。
            return true;
        }

        ApplyPlayerCommand(command.Value);
        return true;
    }

    private void OnFormLoad(object? sender, EventArgs e)
    {
        var loaded = _store.Load();
        _highScore = loaded.Record;

        // 每种情况的提示都控制在 16 字以内（侧栏不许被长句撑破）。
        _noticeLabel.Text = loaded.Status switch
        {
            HighScoreLoadStatus.Missing => "首次运行，暂无存档",
            HighScoreLoadStatus.Ok => "已读取最高分",
            HighScoreLoadStatus.Corrupted => "存档损坏，已忽略",
            _ => "存档读取失败，已忽略",
        };

        _board.HighScore = _highScore?.Score ?? 0;
        StartNewGame();
        _timer.Start();
    }

    private void StartNewGame()
    {
        _engine.Start(Environment.TickCount);
        _keys.Clear();
        _agent?.Reset();
        _board.State = _engine.State;
        _savedThisRound = false;
        _newRecordThisRound = false;
        _pendingMs = 0;
        _lastTick = _clock.Elapsed;
        UpdateHud();
        _board.Invalidate();
    }

    private void OnFrame(object? sender, EventArgs e)
    {
        var now = _clock.Elapsed;
        var nowMs = now.TotalMilliseconds;
        _pendingMs += nowMs - _lastTick.TotalMilliseconds;
        _lastTick = now;

        // 顺序固定：先吃输入，再推进逻辑帧，最后结算/重绘。
        FeedInput(nowMs);

        var steps = 0;
        while (_pendingMs >= FrameMs && steps < MaxCatchUpFrames)
        {
            _engine.Tick();
            _pendingMs -= FrameMs;
            steps++;
        }

        if (steps >= MaxCatchUpFrames)
        {
            _pendingMs = 0;
        }

        SaveHighScoreIfNeeded();

        _board.State = _engine.State;
        _board.HighScore = _highScore?.Score ?? 0;
        _board.IsNewRecord = _newRecordThisRound;
        UpdateHud();
        _board.Invalidate();
    }

    /// <summary>
    /// 把这一帧的意图喂给引擎：玩家来自 <see cref="KeyRepeater"/>，AI 来自 <see cref="IPlayerAgent"/>。
    /// 暂停 / 结束 / 失焦时不积累按键，恢复后不会"瞬移一串"。
    /// </summary>
    private void FeedInput(double nowMs)
    {
        if (_engine.State.Status != GameStatus.Running)
        {
            _keys.Clear();
            return;
        }

        if (_agent is null)
        {
            foreach (var command in _keys.Collect(nowMs))
            {
                _engine.Apply(command);
            }

            return;
        }

        // AI 接管期间：玩家的移动/旋转/硬降一律不生效（暂停键已在 ProcessCmdKey 里单独放行）。
        _keys.Clear();

        try
        {
            _engine.Apply(_agent.NextCommand(_engine.State));
        }
        catch (Exception)
        {
            // AI 契约上不抛异常；真抛了也只能把控制权还给玩家，不能让界面崩掉。
            TurnAgentOff("AI 异常，已切回玩家");
        }
    }

    /// <summary>一次性命令（旋转 / 硬降 / 暂停）与 AI 开关的公共入口。</summary>
    private void ApplyPlayerCommand(GameCommand command)
    {
        if (_agent is not null && command is not (GameCommand.Pause or GameCommand.Resume))
        {
            return;
        }

        // P/Esc 在暂停时应解释为"继续"（引擎只在 Paused 下接受 Resume）。
        if (command == GameCommand.Pause && _engine.State.Status == GameStatus.Paused)
        {
            command = GameCommand.Resume;
        }

        _engine.Apply(command);

        if (_engine.State.Status != GameStatus.Running)
        {
            _keys.Clear();
        }

        _board.State = _engine.State;
        _board.Invalidate();
    }

    /// <summary>F3：在"玩家操作"和"AI 观战"之间切换，棋盘和引擎实例都不换。</summary>
    private void ToggleAgent()
    {
        if (_agent is not null)
        {
            TurnAgentOff("玩家操作（F3 观战）");
            return;
        }

        _agent = new HeuristicAgent(_engine.State.Config, AppBootstrap.CreateRules());
        _agent.Reset();
        _board.ShowAiOverlay = true;
        _board.AiDebug = _agent as IPlayerAgentDebug;
        _noticeLabel.Text = "AI 接管（F3 交回玩家）";
        _board.Invalidate();
        UpdateHud();
    }

    /// <summary>关掉 AI 并把界面恢复成"玩家操作"的样子。</summary>
    private void TurnAgentOff(string? notice)
    {
        _agent = null;
        _board.ShowAiOverlay = false;
        _board.AiDebug = null;
        if (notice is not null)
        {
            _noticeLabel.Text = notice;
        }

        _board.Invalidate();
        UpdateHud();
    }

    /// <summary>暂停 + 清空按键：Alt+Tab、最小化、窗口失焦时调用。</summary>
    private void LeaveGame()
    {
        _keys.Clear();

        if (_engine.State.Status != GameStatus.Running)
        {
            return;
        }

        _engine.Apply(GameCommand.Pause);
        _board.State = _engine.State;
        _board.Invalidate();
    }

    protected override void OnDeactivate(EventArgs e)
    {
        base.OnDeactivate(e);
        LeaveGame();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);

        if (WindowState == FormWindowState.Minimized)
        {
            LeaveGame();
        }
    }

    private void UpdateHud()
    {
        var state = _engine.State;
        _scoreLabel.Text = $"分数  0x{state.Score:X}";
        _highLabel.Text = $"最高  0x{(_highScore?.Score ?? 0):X}";
        _levelLabel.Text = $"等级  {state.Level}";
        _linesLabel.Text = $"消行  {state.LinesCleared}";

        // 预览用图形控件，不再拼文字；队列不够长时对应那格留空。
        var preview = state.PeekNext(_previews.Length);
        for (var i = 0; i < _previews.Length; i++)
        {
            _previews[i].Kind = i < preview.Count ? preview[i] : null;
        }

        UpdateAiLabel();
    }

    private void UpdateAiLabel()
    {
        if (_agent is null)
        {
            _aiLabel.ForeColor = Palette.Muted;
            _aiLabel.Text = "AI  关闭（F3 观战）";
            return;
        }

        _aiLabel.ForeColor = Palette.Accent;

        var debug = _agent as IPlayerAgentDebug;
        var chosen = debug?.Chosen;
        if (chosen is null)
        {
            _aiLabel.Text = $"AI  {_agent.Name}\n决策中…";
            return;
        }

        // 侧栏的一行：评分 | 消行 | 空洞 | 凹凸（全部取自 AI 选中的那个候选）。
        _aiLabel.Text =
            $"评分 {chosen.Score:F1} | 消行 {chosen.ClearedLines} | 空洞 {chosen.Holes} | 凹凸 {chosen.Bumpiness}";
    }

    private void SaveHighScoreIfNeeded()
    {
        var state = _engine.State;
        if (state.Status != GameStatus.GameOver || _savedThisRound)
        {
            return;
        }

        _savedThisRound = true;

        // 0 分的"新纪录"没有意义（首次游玩一开局就死掉的场合），按"没有更好"处理。
        if (state.Score <= (_highScore?.Score ?? 0))
        {
            return;
        }

        var candidate = new HighScore(state.Score, state.Level, state.LinesCleared, DateTimeOffset.Now);

        // 内存里的"最高分"先更新（本局确实破了纪录，遮罩要显示"新纪录！"），
        // 磁盘写失败时由 _noticeLabel 明确告诉玩家"没保存"，下次启动会回到旧纪录。
        _newRecordThisRound = true;
        _highScore = candidate;

        _noticeLabel.Text = _store.Save(candidate)
            ? "新纪录已保存"
            : "存档写入失败，本局未保存";
    }

    private Control BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Palette.Background,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 236f));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        var boardHost = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Palette.Background,
            Padding = new Padding(12),
        };

        _board.Dock = DockStyle.Fill;
        boardHost.Controls.Add(_board);

        root.Controls.Add(boardHost, 0, 0);
        root.Controls.Add(BuildSidebar(), 1, 0);
        return root;
    }

    private Control BuildSidebar()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 9,
            BackColor = Palette.Background,
            Padding = new Padding(4, 14, 12, 10),
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

        // 行高：四个数字 44/36/30/30，"下一个"标题 22，预览区 74，提示占满剩余，AI 40，通知 34。
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 44f));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 36f));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30f));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30f));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 22f));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 74f));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 40f));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34f));

        _scoreLabel = CreateLabel("分数  0x0", 14f, Palette.Accent, FontStyle.Bold);
        _highLabel = CreateLabel("最高  0x0", 11f, Palette.Text, FontStyle.Regular);
        _levelLabel = CreateLabel("等级  1", 11f, Palette.Text, FontStyle.Regular);
        _linesLabel = CreateLabel("消行  0", 11f, Palette.Text, FontStyle.Regular);
        _nextLabel = CreateLabel("下一个", 10f, Palette.Muted, FontStyle.Regular);
        _hintLabel = CreateLabel(
            "← →  左右移动\n↑ / X  顺时针\nZ  逆时针\n↓  软降    空格  硬降\nP / Esc  暂停\nF2  重新开始\nF3  AI 观战 / 接管",
            9.5f,
            Palette.Muted,
            FontStyle.Regular);
        _hintLabel.TextAlign = ContentAlignment.TopLeft;
        _aiLabel = CreateLabel("AI  关闭（F3 观战）", 8.5f, Palette.Muted, FontStyle.Regular);
        _aiLabel.TextAlign = ContentAlignment.TopLeft;
        _noticeLabel = CreateLabel(string.Empty, 8.5f, Palette.Muted, FontStyle.Regular);
        _noticeLabel.TextAlign = ContentAlignment.TopLeft;

        panel.Controls.Add(_scoreLabel, 0, 0);
        panel.Controls.Add(_highLabel, 0, 1);
        panel.Controls.Add(_levelLabel, 0, 2);
        panel.Controls.Add(_linesLabel, 0, 3);
        panel.Controls.Add(_nextLabel, 0, 4);
        panel.Controls.Add(BuildPreviewRow(), 0, 5);
        panel.Controls.Add(_hintLabel, 0, 6);
        panel.Controls.Add(_aiLabel, 0, 7);
        panel.Controls.Add(_noticeLabel, 0, 8);
        return panel;
    }

    /// <summary>并排放 <see cref="GameConfig.PreviewCount"/> 个图形预览（默认 3 个）。</summary>
    private Control BuildPreviewRow()
    {
        var count = Math.Clamp(_engine.State.Config.PreviewCount, 1, 4);
        var row = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = count,
            RowCount = 1,
            BackColor = Palette.Background,
            Margin = Padding.Empty,
        };

        for (var i = 0; i < count; i++)
        {
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        }

        row.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        _previews = new NextPieceView[count];
        for (var i = 0; i < count; i++)
        {
            var view = new NextPieceView
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(3, 2, 3, 2),
                Config = _engine.State.Config,
            };

            _previews[i] = view;
            row.Controls.Add(view, i, 0);
        }

        return row;
    }

    private double NowMs() => _clock.Elapsed.TotalMilliseconds;

    private static Label CreateLabel(string text, float size, Color color, FontStyle style) => new()
    {
        Text = text,
        Dock = DockStyle.Fill,
        AutoSize = false,
        BackColor = Color.Transparent,
        ForeColor = color,
        Font = new Font("Microsoft YaHei UI", size, style),
        TextAlign = ContentAlignment.MiddleLeft,
    };
}
