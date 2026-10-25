namespace Tetris.Core;

/// <summary>
/// 棋盘坐标。<b>X = 列（0 在最左），Y = 行（0 在最上）</b>。
/// 全组统一使用这一约定；发现 [y, x] 写法的代码一律视为 bug。
/// </summary>
public readonly record struct CellPos(int X, int Y)
{
    public CellPos Offset(int dx, int dy) => new(X + dx, Y + dy);

    public override string ToString() => $"({X},{Y})";
}
