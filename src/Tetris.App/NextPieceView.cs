using System.ComponentModel;
using Tetris.Core;

namespace Tetris.App;

/// <summary>
/// "下一个方块"的<b>图形</b>预览（B 负责，任务书 02 §四）。
/// <para>
/// 画出方块的出生姿态（rotation 0），按 <see cref="PieceShape.BoxSize"/>（4×4）包围盒居中，
/// 再按控件大小等比缩放：控件多小方格就多小，不会糊也不会溢出，缩放到 40px 仍能看出形状。
/// </para>
/// <para>
/// 侧栏并排放 <see cref="GameConfig.PreviewCount"/> 个这样的控件（默认 3 个）。
/// 只保存一个 <see cref="PieceKind"/>，<see cref="MainForm"/> 每帧把 <c>PeekNext</c> 的结果塞进来；
/// 值没变时 <see cref="Control.Invalidate()"/> 不触发，所以 60fps 下的重绘开销可以忽略。
/// </para>
/// </summary>
internal sealed class NextPieceView : Control
{
    /// <summary>四周留白（像素），让形状不贴着边框。</summary>
    private const float PaddingPx = 3f;

    private PieceKind? _kind;

    public NextPieceView()
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

    /// <summary>取色用的配置（与引擎共用同一个 <see cref="GameConfig"/> 实例）。</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public GameConfig Config { get; set; } = GameConfig.Default;

    /// <summary>要预览的方块；<c>null</c> 表示这一格没有方块（队列不够长），只画底板。</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public PieceKind? Kind
    {
        get => _kind;
        set
        {
            if (_kind == value)
            {
                return;
            }

            _kind = value;
            Invalidate();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(BackColor);

        // 底板：让 2~3 个预览在侧栏里是一块块可辨识的"卡片"，而不是飘在背景上的方块。
        using (var panel = new SolidBrush(Palette.Board))
        {
            g.FillRectangle(panel, ClientRectangle);
        }

        if (Kind is not { } kind || !Config.Shapes.TryGetValue(kind, out var shape))
        {
            return;
        }

        var size = Math.Min(ClientSize.Width, ClientSize.Height);
        if (size < 8)
        {
            // 控件被压得太小时直接不画，避免出现负数的格子宽度。
            return;
        }

        // 4×4 包围盒居中：格宽 = (短边 - 两侧留白) / 4。
        var cell = (size - 2 * PaddingPx) / PieceShape.BoxSize;
        if (cell < 2f)
        {
            return;
        }

        var box = cell * PieceShape.BoxSize;
        var originX = (ClientSize.Width - box) / 2f;
        var originY = (ClientSize.Height - box) / 2f;

        var brush = Palette.BrushFor(kind);
        using var highlight = new SolidBrush(Color.FromArgb(60, 255, 255, 255));

        foreach (var part in shape.CellsAt(0))
        {
            var rect = new RectangleF(
                originX + part.X * cell + 1f,
                originY + part.Y * cell + 1f,
                Math.Max(1f, cell - 2f),
                Math.Max(1f, cell - 2f));

            g.FillRectangle(brush, rect);

            // 与 BoardView 一致的高光，保证同一块方块的"两块地方看起来是同一个东西"。
            g.FillRectangle(
                highlight,
                new RectangleF(rect.X, rect.Y, rect.Width, Math.Max(1f, rect.Height * 0.18f)));
        }
    }
}
