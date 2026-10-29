using Tetris.Core.Engine;
using Tetris.Core.Rules;

namespace Tetris.Core.Tests;

/// <summary>
/// 真引擎（<see cref="GameEngine"/>）的时序语义：开局、重力、锁定、暂停、结束。
/// 全部用真规则 + 固定种子，断言不依赖墙钟时间；单条用例最多跑几百帧，整套仍是毫秒级。
/// </summary>
public class GameEngineTests
{
    private static GameEngine Engine(int seed = 7) =>
        new(GameConfig.Default, new StandardGameRules(), new XorShiftRandomSource(seed));

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

    /// <summary>连续硬降，收集前 count 个方块的出场顺序。</summary>
    private static List<PieceKind> Take(int seed, int count)
    {
        var engine = Engine(seed);
        engine.Start(seed);

        var kinds = new List<PieceKind>(count);
        for (var i = 0; i < count; i++)
        {
            kinds.Add(engine.State.Active.Kind);
            engine.Apply(GameCommand.HardDrop);
        }

        return kinds;
    }

    /// <summary>把顶部几行的左 11 列填满（留一列，保证不是整行、不会被消掉）。</summary>
    private static void BlockSpawnArea(GameState state, int rows = 4)
    {
        for (var y = 0; y < rows; y++)
        {
            for (var x = 0; x < state.Width - 1; x++)
            {
                state.Board[x, y] = PieceKind.I;
            }
        }
    }

    [Fact]
    public void Start_WithSameSeed_ShouldProduceSameSequence()
    {
        Assert.True(Take(20261029, 12).SequenceEqual(Take(20261029, 12)));
    }

    [Fact]
    public void Start_WithDifferentSeeds_ShouldProduceDifferentSequence()
    {
        Assert.False(Take(1, 12).SequenceEqual(Take(2, 12)));
    }

    [Fact]
    public void Start_ShouldFillPreviewQueueToPreviewCount()
    {
        var engine = Engine();
        engine.Start(3);

        // 出块时先补到 PreviewCount + 1，再出队一个，所以稳定后是 PreviewCount 个
        Assert.Equal(GameConfig.Default.PreviewCount, engine.State.NextQueue.Count);
    }

    [Fact]
    public void Start_ShouldResetBoardAndScore()
    {
        var engine = Engine();
        engine.Start(1);
        engine.Apply(GameCommand.HardDrop);

        engine.Start(2);

        Assert.Equal(0, engine.State.Score);
        Assert.Equal(0, CountCells(engine.State));
        Assert.Equal(GameStatus.Running, engine.State.Status);
    }

    /// <summary>一直推进到方块下落一格，返回用了多少帧（浮点累加会有 1 帧的舍入误差）。</summary>
    private static int TicksUntilDrop(GameEngine engine)
    {
        var top = engine.State.Active.Y;
        for (var i = 1; i <= 200; i++)
        {
            engine.Tick();
            if (engine.State.Active.Y != top)
            {
                return i;
            }
        }

        return -1;
    }

    [Fact]
    public void Tick_AtLevelOne_ShouldDropAfterAboutEightHundredMs()
    {
        var engine = Engine();
        engine.Start(4);

        // 800ms / (1000/60) ≈ 48 帧
        Assert.InRange(TicksUntilDrop(engine), 47, 49);
    }

    [Fact]
    public void Tick_AtHigherLevel_ShouldDropSooner()
    {
        var engine = Engine();
        engine.Start(4);
        engine.State.Level = 13;   // 50ms 一格

        // 50ms / (1000/60) ≈ 3 帧
        Assert.InRange(TicksUntilDrop(engine), 2, 4);
    }

    [Fact]
    public void Tick_WhenGrounded_ShouldLockAndSpawnNext()
    {
        var engine = Engine();
        engine.Start(5);
        engine.State.Level = 13;
        while (engine.Apply(GameCommand.SoftDrop))
        {
        }

        var resting = engine.State.Active;
        for (var i = 0; i < 3; i++)
        {
            engine.Tick();
        }

        Assert.NotSame(resting, engine.State.Active);
        Assert.True(CountCells(engine.State) > 0);
        Assert.Equal(GameStatus.Running, engine.State.Status);
    }

    [Fact]
    public void Tick_WhenPaused_ShouldNotAdvance()
    {
        var engine = Engine();
        engine.Start(6);
        engine.Apply(GameCommand.Pause);
        var frame = engine.State.Frame;

        for (var i = 0; i < 600; i++)
        {
            engine.Tick();
        }

        Assert.Equal(frame, engine.State.Frame);
    }

    [Fact]
    public void Tick_WhenGameOver_ShouldNotAdvance()
    {
        var engine = Engine();
        engine.Start(7);
        BlockSpawnArea(engine.State);
        engine.Apply(GameCommand.HardDrop);
        var frame = engine.State.Frame;

        for (var i = 0; i < 100; i++)
        {
            engine.Tick();
        }

        Assert.Equal(GameStatus.GameOver, engine.State.Status);
        Assert.Equal(frame, engine.State.Frame);
    }

    [Fact]
    public void Apply_SoftDrop_WhenGrounded_ShouldNotLock()
    {
        var engine = Engine();
        engine.Start(8);
        while (engine.Apply(GameCommand.SoftDrop))
        {
        }

        var resting = engine.State.Active;

        Assert.False(engine.Apply(GameCommand.SoftDrop));
        Assert.Same(resting, engine.State.Active);   // 既不锁定也不换块
        Assert.Equal(GameStatus.Running, engine.State.Status);
    }

    [Fact]
    public void Apply_HardDrop_ShouldLockAndSpawnNextWithoutScoring()
    {
        var engine = Engine();
        engine.Start(9);
        var next = engine.State.PeekNext(1)[0];

        Assert.True(engine.Apply(GameCommand.HardDrop));
        Assert.Equal(next, engine.State.Active.Kind);
        Assert.True(CountCells(engine.State) > 0);
        Assert.Equal(0, engine.State.Score);
        Assert.Equal(0, engine.State.LinesCleared);
    }

    [Fact]
    public void Apply_HardDrop_WhenAlreadyGrounded_ShouldStillLock()
    {
        var engine = Engine();
        engine.Start(10);
        while (engine.Apply(GameCommand.SoftDrop))
        {
        }

        Assert.True(engine.Apply(GameCommand.HardDrop));
        Assert.True(CountCells(engine.State) > 0);
    }

    [Fact]
    public void Apply_PauseThenResume_ShouldBlockThenRestoreInput()
    {
        var engine = Engine();
        engine.Start(11);

        Assert.True(engine.Apply(GameCommand.Pause));
        Assert.Equal(GameStatus.Paused, engine.State.Status);
        Assert.False(engine.Apply(GameCommand.MoveLeft));

        Assert.True(engine.Apply(GameCommand.Resume));
        Assert.Equal(GameStatus.Running, engine.State.Status);
        Assert.True(engine.Apply(GameCommand.MoveLeft));
    }

    [Fact]
    public void Apply_WhenReady_ShouldRejectEverything()
    {
        var engine = Engine();

        Assert.Equal(GameStatus.Ready, engine.State.Status);
        Assert.False(engine.Apply(GameCommand.MoveLeft));
        Assert.False(engine.Apply(GameCommand.Pause));
        Assert.False(engine.Apply(GameCommand.Resume));
    }

    [Fact]
    public void Apply_None_ShouldBeIgnored()
    {
        var engine = Engine();
        engine.Start(12);

        Assert.False(engine.Apply(GameCommand.None));
    }

    [Fact]
    public void Apply_WhenSpawnIsBlocked_ShouldEndTheGame()
    {
        var engine = Engine();
        engine.Start(13);
        BlockSpawnArea(engine.State);

        engine.Apply(GameCommand.HardDrop);

        Assert.Equal(GameStatus.GameOver, engine.State.Status);
        Assert.False(engine.Apply(GameCommand.MoveLeft));
    }

    [Fact]
    public void PreviewQueue_ShouldStayAtPreviewCount()
    {
        var engine = Engine();
        engine.Start(14);

        for (var i = 0; i < 8; i++)
        {
            engine.Apply(GameCommand.HardDrop);
            Assert.Equal(GameConfig.Default.PreviewCount, engine.State.NextQueue.Count);
        }
    }

    [Fact]
    public void Tick_ShouldAdvanceFrameOnlyWhileRunning()
    {
        var engine = Engine();
        engine.Start(15);

        engine.Tick();
        Assert.Equal(1, engine.State.Frame);
        engine.Apply(GameCommand.Pause);
        engine.Tick();
        Assert.Equal(1, engine.State.Frame);
    }
}
