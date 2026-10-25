namespace Tetris.Core;

/// <summary>
/// 一种方块的四个旋转态。每个旋转态用 4 行 × 4 列字符描述，'#' 为实心格，其余为空格。
/// 4×4 是<b>包围盒</b>，不是方块实际大小：O 只占 2×2，I 占 1×4，但它们都使用同一个 4×4 盒。
/// 这套字符格式同时就是 config/pieces.json 的形状格式（C 负责加载，A 负责解释）。
/// </summary>
public sealed class PieceShape
{
    /// <summary>包围盒边长，固定 4。</summary>
    public const int BoxSize = 4;

    public PieceKind Kind { get; }

    /// <summary>形如 "#00F0F0" 的颜色，仅用于渲染，逻辑层不关心。</summary>
    public string ColorHex { get; }

    /// <summary>索引 0..3 依次为 0°/90°/180°/270°，每项是"盒内偏移"集合。</summary>
    public IReadOnlyList<IReadOnlyList<CellPos>> Rotations { get; }

    public PieceShape(PieceKind kind, string colorHex, IReadOnlyList<string[]> rotations)
    {
        ArgumentNullException.ThrowIfNull(rotations);
        if (rotations.Count != 4)
        {
            throw new ArgumentException("每种方块必须提供 4 个旋转态。", nameof(rotations));
        }

        Kind = kind;
        ColorHex = string.IsNullOrWhiteSpace(colorHex) ? "#FFFFFF" : colorHex;

        var parsed = new List<IReadOnlyList<CellPos>>(4);
        foreach (var rows in rotations)
        {
            if (rows is null || rows.Length != BoxSize)
            {
                throw new ArgumentException("每个旋转态必须正好 4 行。", nameof(rotations));
            }

            var cells = new List<CellPos>(4);
            for (var y = 0; y < BoxSize; y++)
            {
                var row = rows[y];
                if (row is null || row.Length != BoxSize)
                {
                    throw new ArgumentException("每个旋转态的每一行必须正好 4 个字符。", nameof(rotations));
                }

                for (var x = 0; x < BoxSize; x++)
                {
                    if (row[x] == '#')
                    {
                        cells.Add(new CellPos(x, y));
                    }
                }
            }

            if (cells.Count == 0)
            {
                throw new ArgumentException("旋转态不能为空。", nameof(rotations));
            }

            parsed.Add(cells);
        }

        Rotations = parsed;
    }

    /// <summary>取指定旋转态的盒内偏移（rotation 自动归一化到 0..3）。</summary>
    public IReadOnlyList<CellPos> CellsAt(int rotation) => Rotations[Normalize(rotation)];

    public static int Normalize(int rotation)
    {
        var r = rotation % 4;
        return r < 0 ? r + 4 : r;
    }
}
