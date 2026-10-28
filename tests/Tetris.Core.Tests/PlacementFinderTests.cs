using System.Diagnostics;
using Tetris.AI;
using Tetris.Core;
using Tetris.Core.Tests.Fakes;

namespace Tetris.Core.Tests;

/// <summary>
/// 落点搜索测试：所有候选必须是"真的能放下去"的落点，且搜索过程不能污染传入的状态。
/// </summary>
public class PlacementFinderTests
{
    private static GameState NewState(PieceKind active)
    {
        var state = new GameState(GameConfig.Default) { Status = GameStatus.Running };
        state.Active = new Tetromino(active, state.ShapeOf(active), state.SpawnX(), 0);
        return state;
    }

    [Fact]
    public void FindBest_OnEmptyBoard_ShouldOfferCandidates()
    {
        var finder = new PlacementFinder(new FakeGameRules());
        var state = NewState(PieceKind.T);

        var result = finder.FindBest(state);

        Assert.NotEmpty(result.Candidates);
        Assert.NotNull(result.Best);
        Assert.Contains(result.Best!, result.Candidates);
    }

    [Fact]
    public void FindBest_ShouldNotMutateState()
    {
        var finder = new PlacementFinder(new FakeGameRules());
        var state = NewState(PieceKind.T);
        var boardBefore = (PieceKind?[,])state.Board.Clone();
        var activeBefore = state.Active;

        finder.FindBest(state);

        Assert.Equal(state.Height, state.Board.GetLength(1));
        for (var x = 0; x < state.Width; x++)
        {
            for (var y = 0; y < state.Height; y++)
            {
                Assert.Equal(boardBefore[x, y], state.Board[x, y]);
            }
        }

        Assert.Same(activeBefore, state.Active);
        Assert.Equal(0, state.LinesCleared);
        Assert.Equal(0, state.Score);
    }

    [Fact]
    public void EveryCandidate_ShouldBeLegalAndAlreadyRested()
    {
        var rules = new FakeGameRules();
        var finder = new PlacementFinder(rules);
        var state = NewState(PieceKind.S);

        var result = finder.FindBest(state);

        foreach (var candidate in result.Candidates)
        {
            var piece = state.Active.With(x: candidate.X, y: candidate.Y, rotation: candidate.Rotation);
            Assert.False(rules.Collides(state, piece), $"{candidate} 与已有方块重叠");
            Assert.Equal(0, rules.DropDistance(state, piece));
            Assert.Equal(state.Active.Kind, candidate.Kind);
        }
    }

    [Fact]
    public void Best_ShouldHaveTheHighestScore()
    {
        var finder = new PlacementFinder(new FakeGameRules(), useLookahead: false);
        var state = NewState(PieceKind.L);

        var result = finder.FindBest(state);

        Assert.NotNull(result.Best);
        var max = result.Candidates.Max(c => c.Score);
        Assert.Equal(max, result.Best!.Score, 6);
    }

    [Fact]
    public void FindBest_ShouldPreferThePlacementThatClearsALine()
    {
        var rules = new FakeGameRules();
        var finder = new PlacementFinder(rules, useLookahead: false);
        var state = NewState(PieceKind.O);

        // 底行只差右边两格，O 放进去正好消一行。
        for (var x = 0; x < 10; x++)
        {
            state.Board[x, state.Height - 1] = PieceKind.I;
        }

        var result = finder.FindBest(state);

        Assert.NotNull(result.Best);
        Assert.Equal(1, result.Best!.ClearedLines);
        Assert.Contains(result.Candidates, c => c.ClearedLines == 1);
    }

    [Fact]
    public void FindBest_WhenBoardIsFull_ShouldReturnNoCandidate()
    {
        var finder = new PlacementFinder(new FakeGameRules());
        var state = NewState(PieceKind.T);
        for (var x = 0; x < state.Width; x++)
        {
            for (var y = 0; y < state.Height; y++)
            {
                state.Board[x, y] = PieceKind.O;
            }
        }

        var result = finder.FindBest(state);

        Assert.Empty(result.Candidates);
        Assert.Null(result.Best);
    }

    [Fact]
    public void Lookahead_ShouldNotAddOrDropCandidates()
    {
        var rules = new FakeGameRules();
        var onePly = new PlacementFinder(rules, useLookahead: false);
        var twoPly = new PlacementFinder(rules, useLookahead: true);
        var state = NewState(PieceKind.Z);
        state.NextQueue.Enqueue(PieceKind.I);

        Assert.Equal(
            onePly.FindBest(state).Candidates.Count,
            twoPly.FindBest(state).Candidates.Count);
    }

    [Fact]
    public void ForO_OnEmptyBoard_ShouldOfferTenColumnsPerRotation()
    {
        var finder = new PlacementFinder(new FakeGameRules(), useLookahead: false);
        var state = NewState(PieceKind.O);

        var result = finder.FindBest(state);

        // O 的 4 个旋转态完全相同；盒子有 4 列，O 只占中间两列 → 12 - 2 = 10 个可放列。
        Assert.Equal(40, result.Candidates.Count);
        Assert.Equal(10, result.Candidates.Count(c => c.Rotation == 0));
    }

    [Fact]
    public void EnumeratePlacements_ShouldMatchFindBestWithoutLookahead()
    {
        var rules = new FakeGameRules();
        var finder = new PlacementFinder(rules, useLookahead: false);
        var state = NewState(PieceKind.J);

        var direct = finder.EnumeratePlacements(state, state.Active);
        var searched = finder.FindBest(state).Candidates;

        Assert.Equal(searched.Count, direct.Count);
        Assert.Equal(
            searched.Select(c => (c.Rotation, c.X)).ToArray(),
            direct.Select(c => (c.Rotation, c.X)).ToArray());
    }

    [Fact]
    public void Lookahead_ShouldOnlyChooseAmongNearTiedCandidates()
    {
        var finder = new PlacementFinder(new FakeGameRules());
        var state = NewState(PieceKind.T);
        state.NextQueue.Enqueue(PieceKind.I);

        var result = finder.FindBest(state);

        Assert.NotNull(result.Best);
        var max = result.Candidates.Max(c => c.Score);
        Assert.True(
            result.Best!.Score >= max - PlacementFinder.DefaultLookaheadTolerance - 1e-9,
            $"2-ply 只在并列最优里裁决，不应选比最优低 {max - result.Best.Score:F2} 分的落点");
    }

    [Fact]
    public void FindBestWithLookahead_ShouldStayUnderFrameBudget()
    {
        var finder = new PlacementFinder(new FakeGameRules());
        var state = NewState(PieceKind.T);
        state.NextQueue.Enqueue(PieceKind.I);

        // 预热一次，避免 JIT 时间混进测量。
        finder.FindBest(state);

        var watch = Stopwatch.StartNew();
        const int iterations = 30;
        for (var i = 0; i < iterations; i++)
        {
            finder.FindBest(state);
        }

        watch.Stop();
        var averageMs = watch.Elapsed.TotalMilliseconds / iterations;
        Assert.True(averageMs < 8, $"平均决策耗时 {averageMs:F2}ms，超过 8ms 的帧预算");
    }
}
