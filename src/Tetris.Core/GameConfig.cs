namespace Tetris.Core;

/// <summary>
/// 全局可调参数。默认值就是需求里写死的规格（12 列 × 16 行）。
/// C 的 JSON 加载器产出一个 <see cref="GameConfig"/> 即可支持"改配置不改代码"。
/// </summary>
public sealed class GameConfig
{
    /// <summary>棋盘列数。需求：12。</summary>
    public int BoardWidth { get; init; } = 12;

    /// <summary>棋盘行数。需求：16。</summary>
    public int BoardHeight { get; init; } = 16;

    /// <summary>界面要显示的后续方块个数。</summary>
    public int PreviewCount { get; init; } = 3;

    /// <summary>每升一级需要的消行数。</summary>
    public int LevelUpLines { get; init; } = 10;

    /// <summary>第 1 级到第 N 级的重力间隔（毫秒），超出范围取最后一项。数值越小越快。</summary>
    public IReadOnlyList<int> GravityMsByLevel { get; init; } = new[]
    {
        800, 720, 630, 550, 470, 380, 300, 220, 160, 120, 90, 70, 50,
    };

    public IScoreRule ScoreRule { get; init; } = new StandardScoreRule();

    public IReadOnlyDictionary<PieceKind, PieceShape> Shapes { get; init; } = PieceShapes.CreateDefault();

    /// <summary>面向界面/测试的只读默认实例。</summary>
    public static GameConfig Default { get; } = new();

    public int GravityMsFor(int level)
    {
        if (GravityMsByLevel.Count == 0)
        {
            return 500;
        }

        var index = Math.Clamp(level - 1, 0, GravityMsByLevel.Count - 1);
        return GravityMsByLevel[index];
    }
}
