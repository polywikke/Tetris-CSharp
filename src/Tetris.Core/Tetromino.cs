namespace Tetris.Core;

/// <summary>
/// 正在下落的方块：位置是 <b>4×4 包围盒左上角</b>在棋盘上的坐标。
/// 不可变使用：任何变换都用 <see cref="With"/> 产生新实例，避免 A 的规则代码与 C 的 AI 推演互相改状态。
/// </summary>
public sealed class Tetromino
{
    public Tetromino(PieceKind kind, PieceShape shape, int x = 0, int y = 0, int rotation = 0)
    {
        Kind = kind;
        Shape = shape ?? throw new ArgumentNullException(nameof(shape));
        X = x;
        Y = y;
        Rotation = PieceShape.Normalize(rotation);
    }

    public PieceKind Kind { get; }

    public PieceShape Shape { get; }

    /// <summary>盒左上角的列（可为负，表示部分在左边界外）。</summary>
    public int X { get; }

    /// <summary>盒左上角的行（0 为棋盘顶部）。</summary>
    public int Y { get; }

    /// <summary>0..3，顺时针。</summary>
    public int Rotation { get; }

    /// <summary>当前姿态在棋盘坐标系中的四个格。每次访问都新建数组，调用方随便改。</summary>
    public IReadOnlyList<CellPos> Cells
    {
        get
        {
            var offsets = Shape.CellsAt(Rotation);
            var cells = new CellPos[offsets.Count];
            for (var i = 0; i < offsets.Count; i++)
            {
                cells[i] = new CellPos(offsets[i].X + X, offsets[i].Y + Y);
            }

            return cells;
        }
    }

    public Tetromino With(int? x = null, int? y = null, int? rotation = null) =>
        new(Kind, Shape, x ?? X, y ?? Y, rotation ?? Rotation);

    public Tetromino Clone() => new(Kind, Shape, X, Y, Rotation);

    public override string ToString() => $"{Kind}@({X},{Y})r{Rotation}";
}
