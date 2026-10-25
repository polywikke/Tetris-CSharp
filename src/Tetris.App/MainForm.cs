using System.Diagnostics;
using System.Drawing;
using Tetris.Core;

namespace Tetris.App;

/// <summary>
/// 主窗口（B 负责）：负责"定时器驱动逻辑帧 + 把状态画出来 + 把按键翻译成命令"。
/// 它不认识 A 的规则实现，也不认识 C 的 AI 实现，只认识三个接口。
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
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 15 };
    private readonly Stopwatch _clock = Stopwatch.StartNew();

    private Label _scoreLabel = null!;
    private Label _highLabel = null!;
    private Label _levelLabel = null!;
    private Label _linesLabel = null!;
    private Label _nextLabel = null!;
    private Label _hintLabel = null!;
    private Label _noticeLabel = null!;

    private HighScore? _highScore;
    private TimeSpan _lastTick;
    private double _pendingMs;
    private bool _savedThisRound;

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
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.F2)
        {
            StartNewGame();
            return true;
        }

        var command = KeyMap.ToCommand(keyData);
        if (command is null)
        {
            return base.ProcessCmdKey(ref msg, keyData);
        }

        // P/Esc 在暂停时应解释为"继续"。
        if (command == GameCommand.Pause && _engine.State.Status == GameStatus.Paused)
        {
            command = GameCommand.Resume;
        }

        _engine.Apply(command.Value);
        _board.State = _engine.State;
        _board.Invalidate();
        return true;
    }

    private void OnFormLoad(object? sender, EventArgs e)
    {
        var loaded = _store.Load();
        _highScore = loaded.Record;
        _noticeLabel.Text = loaded.Status switch
        {
            HighScoreLoadStatus.Missing => "首次运行，暂无存档",
            HighScoreLoadStatus.Ok => $"存档：{_store.Location}",
            HighScoreLoadStatus.Corrupted => "存档损坏，已忽略（不影响游戏）",
            _ => "存档读取失败，已忽略（不影响游戏）",
        };

        StartNewGame();
        _timer.Start();
    }

    private void StartNewGame()
    {
        _engine.Start(Environment.TickCount);
        _board.State = _engine.State;
        _savedThisRound = false;
        _pendingMs = 0;
        _lastTick = _clock.Elapsed;
        UpdateHud();
        _board.Invalidate();
    }

    private void OnFrame(object? sender, EventArgs e)
    {
        var now = _clock.Elapsed;
        _pendingMs += (now - _lastTick).TotalMilliseconds;
        _lastTick = now;

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

        _board.State = _engine.State;
        UpdateHud();
        _board.Invalidate();
        SaveHighScoreIfNeeded();
    }

    private void UpdateHud()
    {
        var state = _engine.State;
        _scoreLabel.Text = $"分数  0x{state.Score:X}";
        _highLabel.Text = $"最高  0x{(_highScore?.Score ?? 0):X}";
        _levelLabel.Text = $"等级  {state.Level}";
        _linesLabel.Text = $"消行  {state.LinesCleared}";

        var preview = state.PeekNext(state.Config.PreviewCount);
        _nextLabel.Text = "下一个  " + string.Join(" ", preview);
    }

    private void SaveHighScoreIfNeeded()
    {
        var state = _engine.State;
        if (state.Status != GameStatus.GameOver || _savedThisRound)
        {
            return;
        }

        _savedThisRound = true;
        if (_highScore is not null && state.Score <= _highScore.Score)
        {
            return;
        }

        var candidate = new HighScore(state.Score, state.Level, state.LinesCleared, DateTimeOffset.Now);
        if (_store.Save(candidate))
        {
            _highScore = candidate;
            _noticeLabel.Text = "新纪录已保存";
        }
        else
        {
            _noticeLabel.Text = "存档写入失败，本局成绩未保存";
        }
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
            RowCount = 7,
            BackColor = Palette.Background,
            Padding = new Padding(4, 16, 14, 12),
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        for (var i = 0; i < 5; i++)
        {
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 46f));
        }

        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 46f));

        _scoreLabel = CreateLabel("分数  0x0", 14f, Palette.Accent, FontStyle.Bold);
        _highLabel = CreateLabel("最高  0x0", 11f, Palette.Text, FontStyle.Regular);
        _levelLabel = CreateLabel("等级  1", 11f, Palette.Text, FontStyle.Regular);
        _linesLabel = CreateLabel("消行  0", 11f, Palette.Text, FontStyle.Regular);
        _nextLabel = CreateLabel("下一个  -", 11f, Palette.Text, FontStyle.Regular);
        _hintLabel = CreateLabel(
            "← →  左右移动\n↑ / X  顺时针\nZ  逆时针\n↓  软降    空格  硬降\nP / Esc  暂停\nF2  重新开始",
            9.5f,
            Palette.Muted,
            FontStyle.Regular);
        _hintLabel.TextAlign = ContentAlignment.TopLeft;
        _noticeLabel = CreateLabel(string.Empty, 8.5f, Palette.Muted, FontStyle.Regular);

        panel.Controls.Add(_scoreLabel, 0, 0);
        panel.Controls.Add(_highLabel, 0, 1);
        panel.Controls.Add(_levelLabel, 0, 2);
        panel.Controls.Add(_linesLabel, 0, 3);
        panel.Controls.Add(_nextLabel, 0, 4);
        panel.Controls.Add(_hintLabel, 0, 5);
        panel.Controls.Add(_noticeLabel, 0, 6);
        return panel;
    }

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
