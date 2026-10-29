using Tetris.Core.Rules;

namespace Tetris.Core.Tests;

/// <summary>
/// 真规则（<see cref="StandardGameRules"/>）的碰撞、下落距离、移动、旋转踢墙与锁定消行。
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

    /// <summary>整行填满，可留两个空位给待测方块。</summary>
    private static void FillRow(GameState state, int y, int exceptA = -1, int exceptB = -1)
    {
        for (var x = 0; x < state.Width; x++)
        {
            if (x != exceptA && x != exceptB)
            {
                state.Board[x, y] = PieceKind.I;
            }
        }
    }

    private static int CountCells(GameState state)
    {
        var count = 0;
        foreach (var cell in state.Board)
        {
            if (cell.HasValue)
            {
                count++;
            }
        }

        return count;
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

    [Fact]
    public void TryRotate_WhenFree_ShouldRotateInPlace()
    {
        var state = State(PieceKind.T, y: 5);

        Assert.True(Rules.TryRotate(state, 1));
        Assert.Equal(1, state.Active.Rotation);
        Assert.Equal(4, state.Active.X);
    }

    [Fact]
    public void TryRotate_I_AgainstRightWall_ShouldKickTwoLeft()
    {
        var state = State(PieceKind.I, x: 9, y: 0, rotation: 1);   // 竖 I 贴右墙
        // 转平后占 x..x+3，x=9 越界；I 的第一档踢墙是 -2
        Assert.True(Rules.TryRotate(state, 1));
        Assert.Equal(7, state.Active.X);
        Assert.Equal(2, state.Active.Rotation);
    }

    [Fact]
    public void TryRotate_Jlstz_AgainstRightWall_ShouldKickOneLeft()
    {
        var state = State(PieceKind.T, x: 10, y: 0, rotation: 3);
        // T 的 r3 贴住右墙，转 r0 会多出一列；JLSTZ 的第一档踢墙是 -1
        Assert.True(Rules.TryRotate(state, 1));
        Assert.Equal(9, state.Active.X);
        Assert.Equal(0, state.Active.Rotation);
    }

    [Fact]
    public void TryRotate_WhenNoKickFits_ShouldKeepState()
    {
        var state = State(PieceKind.I, x: 9, y: 0, rotation: 1);
        FillRow(state, 1, 11);   // 第 1、2 行只剩第 11 列可站，横放必然放不下
        FillRow(state, 2, 11);
        var before = state.Active;

        Assert.False(Rules.TryRotate(state, 1));
        Assert.Same(before, state.Active);
    }

    [Fact]
    public void LockActive_ClearingTwoRows_ShouldScoreTwoHundred()
    {
        var state = State(PieceKind.O, x: 3, y: 14);   // 补上第 14、15 行的两个缺口
        FillRow(state, 14, 4, 5);
        FillRow(state, 15, 4, 5);

        var cleared = Rules.LockActive(state);

        Assert.Equal(2, cleared);
        Assert.Equal(200, state.Score);
        Assert.Equal(2, state.LinesCleared);
        Assert.Equal(1, state.Level);
        Assert.Equal(0, state.CountFullRows());
    }

    [Fact]
    public void LockActive_WithVerticalI_ShouldClearFourRows()
    {
        var state = State(PieceKind.I, x: 9, y: 12, rotation: 1);   // 竖 I 占满右侧第 12..15 行缺口
        for (var y = 12; y < state.Height; y++)
        {
            FillRow(state, y, 11);
        }

        Assert.Equal(4, Rules.LockActive(state));
        Assert.Equal(400, state.Score);
    }

    [Fact]
    public void LockActive_AfterTenLines_ShouldLevelUp()
    {
        var state = State(PieceKind.O, x: 3, y: 14);
        state.LinesCleared = 9;   // 再消 2 行即满 10 行
        FillRow(state, 14, 4, 5);
        FillRow(state, 15, 4, 5);

        Rules.LockActive(state);

        Assert.Equal(11, state.LinesCleared);
        Assert.Equal(2, state.Level);
    }

    [Fact]
    public void LockActive_ShouldDropCellsAboveTop()
    {
        var state = State(PieceKind.T, x: 4, y: -1);   // 其中一格探出顶部

        Assert.Equal(0, Rules.LockActive(state));
        Assert.Equal(3, CountCells(state));
    }

    [Fact]
    public void LockActive_WhenLevelUpLinesIsZero_ShouldStayLevelOne()
    {
        var state = new GameState(new GameConfig { LevelUpLines = 0 });
        state.Active = new Tetromino(PieceKind.O, state.ShapeOf(PieceKind.O), 3, 14);
        FillRow(state, 15, 4, 5);   // 只补满第 15 行

        Assert.Equal(1, Rules.LockActive(state));
        Assert.Equal(1, state.Level);   // 不除零、不升级
    }

    [Fact]
    public void NullArguments_ShouldBeRejectedWithoutThrowing()
    {
        Assert.True(Rules.Collides(null!, null!));
        Assert.Equal(0, Rules.DropDistance(null!, null!));
        Assert.False(Rules.TryMove(null!, 1));
        Assert.False(Rules.TryRotate(null!, 1));
        Assert.Equal(0, Rules.LockActive(null!));
    }
}
