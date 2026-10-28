using Tetris.AI;
using Tetris.Core;

namespace Tetris.Core.Tests;

/// <summary>
/// 评估函数测试：四项指标的口径必须和 docs/03-C-AI与测试.md §3.1 一致，
/// 否则 AI 的行为会随实现漂移而无法解释。
/// </summary>
public class BoardEvaluatorTests
{
    [Fact]
    public void EmptyBoard_ShouldHaveZeroMetrics()
    {
        var state = new GameState(GameConfig.Default);

        var metrics = BoardEvaluator.Measure(state);

        Assert.Equal(0, metrics.AggregateHeight);
        Assert.Equal(0, metrics.Holes);
        Assert.Equal(0, metrics.Bumpiness);
        Assert.Equal(0, metrics.ClearedLines);
        Assert.Equal(0d, BoardEvaluator.Score(metrics), 6);
    }

    [Fact]
    public void ColumnHeights_ShouldBeDistanceFromBoardTop()
    {
        var state = new GameState(GameConfig.Default);
        state.Board[0, state.Height - 1] = PieceKind.O;      // 贴底一块 → 高度 1
        state.Board[5, state.Height - 3] = PieceKind.T;      // 悬在离底 3 行 → 高度 3

        var heights = BoardEvaluator.ColumnHeights(state);

        Assert.Equal(1, heights[0]);
        Assert.Equal(3, heights[5]);
        Assert.Equal(0, heights[6]);
    }

    [Fact]
    public void AggregateHeight_ShouldSumEveryColumn()
    {
        var state = new GameState(GameConfig.Default);
        state.Board[0, state.Height - 1] = PieceKind.O;
        state.Board[1, state.Height - 2] = PieceKind.O;

        var metrics = BoardEvaluator.Measure(state);

        Assert.Equal(3, metrics.AggregateHeight);
    }

    [Fact]
    public void Holes_ShouldCountEmptyCellsUnderBlocks()
    {
        var state = new GameState(GameConfig.Default);
        state.Board[0, 0] = PieceKind.O;                     // 顶格一块，下方 15 格全空

        var metrics = BoardEvaluator.Measure(state);

        Assert.Equal(state.Height - 1, metrics.Holes);
    }

    [Fact]
    public void EmptyColumns_ShouldNotCountAsHoles()
    {
        var state = new GameState(GameConfig.Default);

        Assert.Equal(0, BoardEvaluator.CountHoles(state));
    }

    [Fact]
    public void Bumpiness_ShouldSumAdjacentHeightDifferences()
    {
        var state = new GameState(GameConfig.Default);
        state.Board[0, state.Height - 1] = PieceKind.O;          // 高 1
        state.Board[1, state.Height - 2] = PieceKind.O;          // 高 2
        state.Board[2, state.Height - 4] = PieceKind.O;          // 高 4

        var metrics = BoardEvaluator.Measure(state);

        // |1-2| + |2-4| + |4-0|（第 2 列到第 3 列的落差）+ 其余相邻列为 0 = 7
        Assert.Equal(7, metrics.Bumpiness);
    }

    [Fact]
    public void Score_ShouldUseDocumentedWeights()
    {
        var metrics = new BoardMetrics(AggregateHeight: 10, Holes: 2, Bumpiness: 3, ClearedLines: 1);

        var score = BoardEvaluator.Score(metrics);

        // 0.76*1 - 0.51*10 - 0.36*2 - 0.18*3
        Assert.Equal(-5.6, score, 6);
    }

    [Fact]
    public void ClearedLines_ShouldBeTheOnlyPositiveFeature()
    {
        var withoutClear = BoardEvaluator.Score(new BoardMetrics(10, 2, 3, 0));
        var withOneClear = BoardEvaluator.Score(new BoardMetrics(10, 2, 3, 1));

        Assert.Equal(0.76, withOneClear - withoutClear, 6);
        Assert.True(withOneClear > withoutClear);
    }

    [Fact]
    public void NegativeClearedLines_ShouldBeClampedToZero()
    {
        var state = new GameState(GameConfig.Default);

        var metrics = BoardEvaluator.Measure(state, clearedLines: -3);

        Assert.Equal(0, metrics.ClearedLines);
    }

    [Fact]
    public void Evaluate_ShouldEqualScoreOfMeasure()
    {
        var state = new GameState(GameConfig.Default);
        state.Board[3, state.Height - 2] = PieceKind.I;

        Assert.Equal(
            BoardEvaluator.Score(BoardEvaluator.Measure(state, 2)),
            BoardEvaluator.Evaluate(state, 2),
            6);
    }
}
