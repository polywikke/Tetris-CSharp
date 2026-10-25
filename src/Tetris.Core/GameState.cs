namespace Tetris.Core;

/// <summary>
/// 一局游戏的完整状态。它是全组唯一共享的数据结构：
/// <list type="bullet">
/// <item>A 在引擎内部读写；</item>
/// <item>B 的界面<b>只读</b>；</item>
/// <item>C 只能先 <see cref="Clone"/> 再在副本上推演。</item>
/// </list>
/// 状态必须能脱离界面独立 new 出来、也能完整复制（评分点：状态可独立创建和复制）。
/// </summary>
public sealed class GameState
{
    public GameState(GameConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        Config = config;
        Board = new PieceKind?[config.BoardWidth, config.BoardHeight];
        NextQueue = new Queue<PieceKind>();
        Active = new Tetromino(PieceKind.T, config.Shapes[PieceKind.T], SpawnX(), 0);
    }

    /// <summary>深拷贝构造。B 用于快照、C 用于 AI 推演，两者都不会影响原状态。</summary>
    private GameState(GameState source)
    {
        Config = source.Config;
        Board = (PieceKind?[,])source.Board.Clone();
        Active = source.Active.Clone();
        NextQueue = new Queue<PieceKind>(source.NextQueue);
        Score = source.Score;
        Level = source.Level;
        LinesCleared = source.LinesCleared;
        Status = source.Status;
        Frame = source.Frame;
    }

    public GameConfig Config { get; }

    public int Width => Config.BoardWidth;

    public int Height => Config.BoardHeight;

    /// <summary>已固定的方块，索引方式 <c>Board[x, y]</c>；null 表示空。</summary>
    public PieceKind?[,] Board { get; }

    /// <summary>当前下落的方块。引擎保证它始终非 null。</summary>
    public Tetromino Active { get; set; }

    /// <summary>接下来的方块（7-bag 抽出的顺序）。界面只读前 <see cref="GameConfig.PreviewCount"/> 个。</summary>
    public Queue<PieceKind> NextQueue { get; }

    /// <summary>十进制分数；<b>十六进制显示是界面的责任</b>（B 用 <c>Score:X</c> 格式化）。</summary>
    public int Score { get; set; }

    public int Level { get; set; } = 1;

    public int LinesCleared { get; set; }

    public GameStatus Status { get; set; } = GameStatus.Ready;

    /// <summary>已经过的逻辑帧数，仅用于调试与回放。</summary>
    public long Frame { get; set; }

    public GameState Clone() => new(this);

    public PieceShape ShapeOf(PieceKind kind) => Config.Shapes[kind];

    /// <summary>新方块默认生成列（包围盒左上角），使方块在棋盘水平居中。</summary>
    public int SpawnX() => (Width - PieceShape.BoxSize) / 2;

    public bool IsInside(int x, int y) => x >= 0 && x < Width && y >= 0 && y < Height;

    /// <summary>坐标在棋盘内且已被固定方块占据。边界外返回 false（边界判断请用 <see cref="IsInside"/>）。</summary>
    public bool IsOccupied(int x, int y) => IsInside(x, y) && Board[x, y].HasValue;

    public bool IsRowFull(int y)
    {
        if (y < 0 || y >= Height)
        {
            return false;
        }

        for (var x = 0; x < Width; x++)
        {
            if (!Board[x, y].HasValue)
            {
                return false;
            }
        }

        return true;
    }

    public int CountFullRows()
    {
        var count = 0;
        for (var y = 0; y < Height; y++)
        {
            if (IsRowFull(y))
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>取接下来 count 个方块（不改动队列）。count 超过队列长度时返回现有的全部。</summary>
    public IReadOnlyList<PieceKind> PeekNext(int count)
    {
        if (count <= 0)
        {
            return Array.Empty<PieceKind>();
        }

        var take = Math.Min(count, NextQueue.Count);
        var result = new PieceKind[take];
        var i = 0;
        foreach (var kind in NextQueue)
        {
            if (i >= take)
            {
                break;
            }

            result[i++] = kind;
        }

        return result;
    }

    /// <summary>清空棋盘（开局/重开用），不改变 Config。</summary>
    public void ClearBoard() => Array.Clear(Board, 0, Board.Length);
}
