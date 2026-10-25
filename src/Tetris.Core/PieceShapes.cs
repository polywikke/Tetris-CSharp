namespace Tetris.Core;

/// <summary>
/// 标准 7 种方块的形状数据（SRS 朝向）。这是<b>数据</b>，不是算法：
/// 碰撞、踢墙、消行、计分都在 <see cref="IGameRules"/> 的实现里。
/// 想换方块布局或从 JSON 读配置时，构造同样的 <see cref="PieceShape"/> 即可，不需要改这里。
/// </summary>
public static class PieceShapes
{
    /// <summary>每次调用返回一份新的形状字典（避免多个 GameConfig 共享可变对象）。</summary>
    public static IReadOnlyDictionary<PieceKind, PieceShape> CreateDefault()
    {
        var shapes = new Dictionary<PieceKind, PieceShape>(7)
        {
            // I：横躺在盒内第 1 行
            [PieceKind.I] = Make(PieceKind.I, "#00E5FF",
                "....|####|....|....",
                "..#.|..#.|..#.|..#.",
                "....|....|####|....",
                ".#..|.#..|.#..|.#.."),

            // O：四个旋转态完全相同
            [PieceKind.O] = Make(PieceKind.O, "#FFD400",
                ".##.|.##.|....|....",
                ".##.|.##.|....|....",
                ".##.|.##.|....|....",
                ".##.|.##.|....|...."),

            // T
            [PieceKind.T] = Make(PieceKind.T, "#B14CFF",
                ".#..|###.|....|....",
                ".#..|.##.|.#..|....",
                "....|###.|.#..|....",
                ".#..|##..|.#..|...."),

            // S
            [PieceKind.S] = Make(PieceKind.S, "#3DDC84",
                ".##.|##..|....|....",
                ".#..|.##.|..#.|....",
                "....|.##.|##..|....",
                "#...|##..|.#..|...."),

            // Z
            [PieceKind.Z] = Make(PieceKind.Z, "#FF4D4D",
                "##..|.##.|....|....",
                "..#.|.##.|.#..|....",
                "....|##..|.##.|....",
                ".#..|##..|#...|...."),

            // J
            [PieceKind.J] = Make(PieceKind.J, "#4D79FF",
                "#...|###.|....|....",
                ".##.|.#..|.#..|....",
                "....|###.|..#.|....",
                ".#..|.#..|##..|...."),

            // L
            [PieceKind.L] = Make(PieceKind.L, "#FF9F1C",
                "..#.|###.|....|....",
                ".#..|.#..|.##.|....",
                "....|###.|#...|....",
                "##..|.#..|.#..|...."),
        };

        return shapes;
    }

    /// <summary>把 "....|####|....|...." 这种一行 4 段的写法拆成 4 个字符串。</summary>
    private static PieceShape Make(PieceKind kind, string color, string r0, string r1, string r2, string r3)
    {
        static string[] Split(string art)
        {
            var rows = art.Split('|');
            if (rows.Length != PieceShape.BoxSize)
            {
                throw new ArgumentException($"形状描述必须用 '|' 分成 4 行：{art}");
            }

            return rows;
        }

        return new PieceShape(kind, color, new[] { Split(r0), Split(r1), Split(r2), Split(r3) });
    }
}
