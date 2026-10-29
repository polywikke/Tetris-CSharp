using System.Drawing;
using System.Text;
using Tetris.Core;

namespace Tetris.App;

/// <summary>
/// 棋盘绘制控件（B 负责）。只读 <see cref="GameState"/>，不修改它；
/// 尺寸随窗口自适应，缩放时按可用区域重新计算格子大小。
/// <para>
/// 除棋盘本身外还负责三层"叠加信息"：
/// <list type="number">
/// <item>暂停 / 游戏结束的遮罩（结束遮罩带得分、最高分，破纪录时多一行"新纪录！"）；</item>
/// <item>F3 观战时的 AI 候选热力图：候选落点按 <c>Score</c> 归一化上色（低分红、高分绿），
/// AI 真正选中的那个用高亮描边勾出来；</item>
/// <item>候选为空、暂停、结束时不画热力图，避免留下上一帧的"幽灵方块"。</item>
/// </list>
/// </para>
/// </summary>
internal sealed class BoardView : Control
{
    private const float PaddingPx = 8f;

    /// <summary>热力图最差候选的颜色（红）。</summary>
    private static readonly Color HeatLow = Color.FromArgb(232, 82, 74);

    /// <summary>热力图最好候选的颜色（绿）。</summary>
    private static readonly Color HeatHigh = Color.FromArgb(86, 226, 104);

    /// <summary>要绘制的状态。由 MainForm 每帧赋值，不参与设计器序列化。</summary>
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public GameState? State { get; set; }

    /// <summary>当前最高分（侧栏与结束遮罩共用），0 表示还没有记录。</summary>
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public int HighScore { get; set; }

    /// <summary>本局是否破了纪录：结束时遮罩多一句"新纪录！"。</summary>
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool IsNewRecord { get; set; }

    /// <summary>是否画 AI 候选热力图（F3 打开 AI 时为 true）。</summary>
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool ShowAiOverlay { get; set; }

    /// <summary>AI 的调试视图（<see cref="IPlayerAgentDebug"/>）。为 null 时不画热力图。</summary>
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public IPlayerAgentDebug? AiDebug { get; set; }

    public BoardView()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint
            | ControlStyles.UserPaint
            | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.ResizeRedraw,
            true);
        DoubleBuffered = true;
        BackColor = Palette.Background;
        TabStop = false;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var state = State;
        var g = e.Graphics;
        g.Clear(Palette.Background);
        if (state is null)
        {
            return;
        }

        var cell = Math.Min(
            (ClientSize.Width - 2 * PaddingPx) / state.Width,
            (ClientSize.Height - 2 * PaddingPx) / state.Height);

        // 窗口被拖动到极小尺寸时直接放弃绘制，避免除零和异常。
        if (cell < 3)
        {
            return;
        }

        var boardWidth = cell * state.Width;
        var boardHeight = cell * state.Height;
        var originX = (ClientSize.Width - boardWidth) / 2f;
        var originY = (ClientSize.Height - boardHeight) / 2f;

        using (var background = new SolidBrush(Palette.Board))
        {
            g.FillRectangle(background, originX, originY, boardWidth, boardHeight);
        }

        using (var gridPen = new Pen(Palette.Grid, 1f))
        {
            for (var i = 1; i < state.Width; i++)
            {
                var x = originX + i * cell;
                g.DrawLine(gridPen, x, originY, x, originY + boardHeight);
            }

            for (var j = 1; j < state.Height; j++)
            {
                var y = originY + j * cell;
                g.DrawLine(gridPen, originX, y, originX + boardWidth, y);
            }
        }

        for (var y = 0; y < state.Height; y++)
        {
            for (var x = 0; x < state.Width; x++)
            {
                var kind = state.Board[x, y];
                if (kind.HasValue)
                {
                    DrawCell(g, originX, originY, cell, x, y, kind.Value);
                }
            }
        }

        foreach (var part in state.Active.Cells)
        {
            if (part.X >= 0 && part.X < state.Width && part.Y >= 0 && part.Y < state.Height)
            {
                DrawCell(g, originX, originY, cell, part.X, part.Y, state.Active.Kind);
            }
        }

        // AI 热力图只在对局进行中画：暂停/结束时保留上一帧的候选会让人误以为 AI 还在思考。
        if (ShowAiOverlay && state.Status == GameStatus.Running)
        {
            DrawAiCandidates(g, originX, originY, cell, state);
        }

        using (var borderPen = new Pen(Palette.Border, 2f))
        {
            g.DrawRectangle(borderPen, originX, originY, boardWidth, boardHeight);
        }

        var overlay = BuildOverlayText(state);
        if (overlay is not null)
        {
            DrawOverlay(g, overlay, state.Status == GameStatus.GameOver ? 13f : 15f);
        }
    }

    private static void DrawCell(Graphics g, float originX, float originY, float cell, int x, int y, PieceKind kind)
    {
        var rect = new RectangleF(
            originX + x * cell + 1f,
            originY + y * cell + 1f,
            Math.Max(1f, cell - 2f),
            Math.Max(1f, cell - 2f));

        g.FillRectangle(Palette.BrushFor(kind), rect);

        using var highlight = new SolidBrush(Color.FromArgb(60, 255, 255, 255));
        g.FillRectangle(highlight, new RectangleF(rect.X, rect.Y, rect.Width, Math.Max(1f, rect.Height * 0.18f)));
    }

    /// <summary>按状态拼遮罩文字；返回 null 表示不画遮罩（Running）。</summary>
    private string? BuildOverlayText(GameState state)
    {
        switch (state.Status)
        {
            case GameStatus.Paused:
                return "已暂停（P 继续）";

            case GameStatus.Ready:
                return "按 P 开始";

            case GameStatus.GameOver:
            {
                var text = new StringBuilder();
                text.Append("游戏结束\n");
                text.Append("得分 0x").Append(state.Score.ToString("X"));
                text.Append("    最高 0x").Append(HighScore.ToString("X"));
                if (IsNewRecord)
                {
                    text.Append("\n新纪录！");
                }

                text.Append("\nF2 重新开始");
                return text.ToString();
            }

            default:
                return null;
        }
    }

    /// <summary>
    /// 画 AI 候选热力图。候选是"落点"（4×4 盒左上角 + 旋转态），每个格子画一个半透明色块；
    /// 颜色只取决于候选之间的相对高低（同一帧内归一化），因此换一块方块也能看出"哪几个明显更好"。
    /// </summary>
    private void DrawAiCandidates(Graphics g, float originX, float originY, float cell, GameState state)
    {
        var debug = AiDebug;
        if (debug is null)
        {
            return;
        }

        var candidates = debug.Candidates;
        if (candidates is null || candidates.Count == 0)
        {
            return;
        }

        var min = double.MaxValue;
        var max = double.MinValue;
        foreach (var candidate in candidates)
        {
            if (candidate.Score < min)
            {
                min = candidate.Score;
            }

            if (candidate.Score > max)
            {
                max = candidate.Score;
            }
        }

        var span = max - min;
        var chosen = debug.Chosen;

        // 先画落选的（半透明），最后画选中的，保证高亮那一个总在最上层。
        foreach (var candidate in candidates)
        {
            if (chosen is not null && candidate.Equals(chosen))
            {
                continue;
            }

            DrawCandidate(g, originX, originY, cell, state, candidate, Normalize(candidate, min, span), false);
        }

        if (chosen is not null)
        {
            DrawCandidate(g, originX, originY, cell, state, chosen, Normalize(chosen, min, span), true);
        }
    }

    /// <summary>把 Score 映射到 0..1：1 是本帧最高分。</summary>
    private static double Normalize(AgentCandidate candidate, double min, double span)
    {
        if (span <= 0)
        {
            return 1d;
        }

        return Math.Clamp((candidate.Score - min) / span, 0d, 1d);
    }

    private static void DrawCandidate(
        Graphics g,
        float originX,
        float originY,
        float cell,
        GameState state,
        AgentCandidate candidate,
        double quality,
        bool highlight)
    {
        if (!state.Config.Shapes.TryGetValue(candidate.Kind, out var shape))
        {
            return;
        }

        // 红(低分) → 绿(高分)：两段线性插值，中间自然过渡成偏黄。
        var color = Mix(HeatLow, HeatHigh, quality);
        var fillAlpha = highlight ? 110 : 58;
        var edgeAlpha = highlight ? 240 : 110;

        using var fill = new SolidBrush(Color.FromArgb(fillAlpha, color));
        using var edge = new Pen(
            Color.FromArgb(edgeAlpha, color),
            highlight ? Math.Max(2f, cell * 0.12f) : 1f);

        foreach (var part in shape.CellsAt(candidate.Rotation))
        {
            var x = candidate.X + part.X;
            var y = candidate.Y + part.Y;
            if (x < 0 || x >= state.Width || y < 0 || y >= state.Height)
            {
                continue;
            }

            var rect = new RectangleF(
                originX + x * cell + 1.5f,
                originY + y * cell + 1.5f,
                Math.Max(1f, cell - 3f),
                Math.Max(1f, cell - 3f));

            g.FillRectangle(fill, rect);
            g.DrawRectangle(edge, rect.X, rect.Y, rect.Width, rect.Height);
        }

        if (!highlight)
        {
            return;
        }

        // 选中落点再套一圈强调色描边，缩到小窗口也一眼能找到。
        using var chosenPen = new Pen(Palette.Accent, Math.Max(2f, cell * 0.14f));
        foreach (var part in shape.CellsAt(candidate.Rotation))
        {
            var x = candidate.X + part.X;
            var y = candidate.Y + part.Y;
            if (x < 0 || x >= state.Width || y < 0 || y >= state.Height)
            {
                continue;
            }

            var rect = new RectangleF(
                originX + x * cell + 0.75f,
                originY + y * cell + 0.75f,
                Math.Max(1f, cell - 1.5f),
                Math.Max(1f, cell - 1.5f));

            g.DrawRectangle(chosenPen, rect.X, rect.Y, rect.Width, rect.Height);
        }
    }

    /// <summary>两个颜色按 0..1 混合（quality = 0 取 <paramref name="from"/>）。</summary>
    private static Color Mix(Color from, Color to, double quality)
    {
        var t = Math.Clamp(quality, 0d, 1d);
        return Color.FromArgb(
            (int)Math.Round(from.R + (to.R - from.R) * t),
            (int)Math.Round(from.G + (to.G - from.G) * t),
            (int)Math.Round(from.B + (to.B - from.B) * t));
    }

    private void DrawOverlay(Graphics g, string text, float fontSize)
    {
        using var shade = new SolidBrush(Color.FromArgb(170, 0, 0, 0));
        g.FillRectangle(shade, ClientRectangle);

        using var font = new Font("Microsoft YaHei UI", fontSize, FontStyle.Bold);
        using var brush = new SolidBrush(Palette.Text);
        using var format = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
        };

        g.DrawString(text, font, brush, ClientRectangle, format);
    }
}
