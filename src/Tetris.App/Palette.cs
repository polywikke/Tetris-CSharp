using System.Drawing;
using Tetris.Core;

namespace Tetris.App;

/// <summary>界面配色与方块画刷（B 负责）。颜色从 <see cref="PieceShape.ColorHex"/> 来，改 JSON 即可换肤。</summary>
internal static class Palette
{
    public static readonly Color Background = Color.FromArgb(16, 16, 22);

    public static readonly Color Board = Color.FromArgb(26, 26, 34);

    public static readonly Color Grid = Color.FromArgb(44, 44, 58);

    public static readonly Color Border = Color.FromArgb(92, 92, 128);

    public static readonly Color Text = Color.FromArgb(232, 232, 240);

    public static readonly Color Muted = Color.FromArgb(140, 140, 160);

    public static readonly Color Accent = Color.FromArgb(120, 210, 255);

    private static readonly Dictionary<PieceKind, SolidBrush> Brushes = CreateBrushes();

    public static Brush BrushFor(PieceKind kind) => Brushes[kind];

    private static Dictionary<PieceKind, SolidBrush> CreateBrushes()
    {
        var result = new Dictionary<PieceKind, SolidBrush>();
        foreach (var (kind, shape) in GameConfig.Default.Shapes)
        {
            result[kind] = new SolidBrush(ColorTranslator.FromHtml(shape.ColorHex));
        }

        return result;
    }
}
