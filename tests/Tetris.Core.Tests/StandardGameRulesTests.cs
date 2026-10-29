using Tetris.Core.Rules;

namespace Tetris.Core.Tests;

/// <summary>
/// 真规则（<see cref="StandardGameRules"/>）的碰撞、下落距离与移动。
/// 与 <c>FakeGameRules</c> 的分工：那个是 AI 的测试替身，这里验的是交付实现本身。
/// </summary>
public class StandardGameRulesTests
{
    private static readonly StandardGameRules Rules = new();

    /// <summary>空棋盘 + 指定当前方块，避免每个用例重复样板。</summary>
    private static GameState State(PieceKind kind = PieceKind.T, int x = 4, int y = 0, int rotation = 0)
    {
        var state = new GameState(GameConfig.Default);
        state.Active = new Tetromino(kind, state.ShapeOf(kind), x, y, rotation);
        return state;
    }

    private static void FillRow(GameState state, int y)
    {
        for (var x = 0; x < state.Width; x++)
        {
            state.Board[x, y] = PieceKind.I;
        }
    }

    [Fact]
    public void Collides_OnEmptyBoard_ShouldBeFalse()
    {
        var state = State();

        Assert.False(Rules.Collides(state, state.Active));
    }

    [Fact]
    public void Collides_PastLeftWall_ShouldBeTrue()
    {
        var state = State(PieceKind.I, x: -1);   // 横放 I 占 x-1..x+2

        Assert.True(Rules.Collides(state, state.Active));
    }

    [Fact]
    public void Collides_BelowFloor_ShouldBeTrue()
    {
        var state = State(PieceKind.O, y: 15);   // O 占 y..y+1，末行越界

        Assert.True(Rules.Collides(state, state.Active));
    }

    [Fact]
    public void Collides_AboveBoardTop_ShouldBeFalse()
    {
        var state = State(PieceKind.T, y: -1);   // 允许探出顶部

        Assert.False(Rules.Collides(state, state.Active));
    }

    [Fact]
    public void Collides_OnLockedCell_ShouldBeTrue()
    {
        var state = State(PieceKind.O, x: 4, y: 4);   // O 占 (5,4)(6,4)(5,5)(6,5)
        state.Board[5, 5] = PieceKind.Z;

        Assert.True(Rules.Collides(state, state.Active));
    }

    [Fact]
    public void DropDistance_OnEmptyBoard_ShouldRestOnFloor()
    {
        var state = State(PieceKind.O);   // 高 2，贴底时占满第 14、15 行

        Assert.Equal(state.Height - 2, Rules.DropDistance(state, state.Active));
    }

    [Fact]
    public void DropDistance_OnStack_ShouldStopAboveIt()
    {
        var state = State(PieceKind.O);
        FillRow(state, 10);

        Assert.Equal(8, Rules.DropDistance(state, state.Active));   // 底部停在 9
    }

    [Fact]
    public void DropDistance_WhenGrounded_ShouldBeZero()
    {
        var state = State(PieceKind.O, y: 14);

        Assert.Equal(0, Rules.DropDistance(state, state.Active));
    }

    [Fact]
    public void TryMove_WhenFree_ShouldShiftActive()
    {
        var state = State();

        Assert.True(Rules.TryMove(state, 1));
        Assert.Equal(5, state.Active.X);
    }

    [Fact]
    public void TryMove_WhenBlocked_ShouldKeepState()
    {
        var state = State(PieceKind.I, x: 0);   // 已贴左墙
        var before = state.Active;

        Assert.False(Rules.TryMove(state, -1));
        Assert.Same(before, state.Active);
    }
}
