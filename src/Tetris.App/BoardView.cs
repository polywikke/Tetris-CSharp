using System.Drawing;
using Tetris.Core;

namespace Tetris.App;

/// <summary>
/// 棋盘绘制控件（B 负责）。只读 <see cref="GameState"/>，不修改它；
/// 尺寸随窗口自适应，缩放时按可用区域重新计算格子大小。
/// </summary>
internal sealed class BoardView : Control
{
    private const float PaddingPx = 8f;

    /// <summary>要绘制的状态。由 MainForm 每帧赋值，不参与设计器序列化。</summary>
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public GameState? State { get; set; }

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

        using (var borderPen = new Pen(Palette.Border, 2f))
        {
            g.DrawRectangle(borderPen, originX, originY, boardWidth, boardHeight);
        }

        var overlay = state.Status switch
        {
            GameStatus.Paused => "已暂停（P 继续）",
            GameStatus.GameOver => "游戏结束（F2 重开）",
            GameStatus.Ready => "按 P 开始",
            _ => null,
        };

        if (overlay is not null)
        {
            DrawOverlay(g, overlay);
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

    private void DrawOverlay(Graphics g, string text)
    {
        using var shade = new SolidBrush(Color.FromArgb(170, 0, 0, 0));
        g.FillRectangle(shade, ClientRectangle);

        using var font = new Font("Microsoft YaHei UI", 15f, FontStyle.Bold);
        using var brush = new SolidBrush(Palette.Text);
        using var format = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
        };

        g.DrawString(text, font, brush, ClientRectangle, format);
    }
}
