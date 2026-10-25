namespace Tetris.Core.Tests;

/// <summary>
/// 契约测试（C 维护，A 的实现必须让它们一直绿）。
/// 这些用例只依赖 <b>已经冻结的接口</b>，所以 D1 就能全部通过；
/// 真正的碰撞/旋转/消行/计分测试由 C 在 A 交付规则后继续补到 15 个以上。
/// </summary>
public class ContractTests
{
    [Fact]
    public void DefaultConfig_ShouldMatchRequiredBoardSize()
    {
        var config = GameConfig.Default;

        Assert.Equal(12, config.BoardWidth);
        Assert.Equal(16, config.BoardHeight);
    }

    [Fact]
    public void GameState_CanBeCreatedWithoutAnyEngineOrUi()
    {
        var state = new GameState(GameConfig.Default);

        Assert.Equal(12, state.Width);
        Assert.Equal(16, state.Height);
        Assert.Equal(GameStatus.Ready, state.Status);
        Assert.Equal(4, state.Active.Cells.Count);
    }

    [Fact]
    public void Clone_ShouldDeepCopyBoard()
    {
        var state = new GameState(GameConfig.Default);
        var copy = state.Clone();

        copy.Board[0, 0] = PieceKind.O;

        Assert.NotSame(state.Board, copy.Board);
        Assert.Null(state.Board[0, 0]);
        Assert.Equal(PieceKind.O, copy.Board[0, 0]);
    }

    [Fact]
    public void Clone_ShouldCopyQueueAndActivePiece()
    {
        var state = new GameState(GameConfig.Default);
        state.NextQueue.Enqueue(PieceKind.I);

        var copy = state.Clone();
        copy.NextQueue.Enqueue(PieceKind.O);

        Assert.Single(state.NextQueue);
        Assert.Equal(2, copy.NextQueue.Count);
        Assert.NotSame(state.Active, copy.Active);
        Assert.Equal(state.Active.Kind, copy.Active.Kind);
    }

    [Fact]
    public void ScoreRule_ShouldAwardHundredPerClearedLine()
    {
        var rule = new StandardScoreRule();

        Assert.Equal(100, rule.ScoreFor(1, 1));
        Assert.Equal(200, rule.ScoreFor(2, 1));
        Assert.Equal(400, rule.ScoreFor(4, 9));
        Assert.Equal(0, rule.ScoreFor(0, 1));
    }

    [Fact]
    public void EveryPiece_ShouldHaveFourRotationsOfFourCells()
    {
        foreach (var (kind, shape) in PieceShapes.CreateDefault())
        {
            Assert.Equal(4, shape.Rotations.Count);
            foreach (var cells in shape.Rotations)
            {
                Assert.Equal(4, cells.Count);
            }

            Assert.False(string.IsNullOrWhiteSpace(shape.ColorHex), $"{kind} 缺少颜色");
        }
    }

    [Fact]
    public void FreshPiece_ShouldSpawnInsideBoard()
    {
        var state = new GameState(GameConfig.Default);

        Assert.All(state.Active.Cells, cell =>
        {
            Assert.InRange(cell.X, 0, state.Width - 1);
            Assert.InRange(cell.Y, 0, state.Height - 1);
        });
    }

    [Fact]
    public void XorShift_ShouldBeReproducibleForSameSeed()
    {
        var a = new XorShiftRandomSource(20261025);
        var b = new XorShiftRandomSource(20261025);

        for (var i = 0; i < 50; i++)
        {
            Assert.Equal(a.Next(7), b.Next(7));
        }
    }
}
