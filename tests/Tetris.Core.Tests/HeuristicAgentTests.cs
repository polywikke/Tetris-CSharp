using Tetris.AI;
using Tetris.Core;
using Tetris.Core.Tests.Fakes;

namespace Tetris.Core.Tests;

/// <summary>
/// 指令状态机测试：AI 每帧只能发一条指令，且必须保证"永不抛异常、永不卡死"。
/// 需要真实规则的场景用 <see cref="SimulatedGame"/> 跑整局，避免依赖 A 的实现进度。
/// </summary>
public class HeuristicAgentTests
{
    private static HeuristicAgent NewAgent(GameState state) =>
        new(state.Config, new FakeGameRules());

    [Fact]
    public void BeforeAnyDecision_CandidatesShouldBeEmptyAndChosenNull()
    {
        var state = new GameState(GameConfig.Default);
        var agent = NewAgent(state);

        Assert.Empty(agent.Candidates);
        Assert.Null(agent.Chosen);
        Assert.Null(agent.Plan);
    }

    [Fact]
    public void NextCommand_WhenGameIsReady_ShouldReturnNone()
    {
        var state = new GameState(GameConfig.Default);
        var agent = NewAgent(state);

        Assert.Equal(GameCommand.None, agent.NextCommand(state));
    }

    [Fact]
    public void NextCommand_WhenPaused_ShouldReturnNone()
    {
        var state = new GameState(GameConfig.Default) { Status = GameStatus.Paused };
        var agent = NewAgent(state);

        Assert.Equal(GameCommand.None, agent.NextCommand(state));
    }

    [Fact]
    public void NextCommand_WhenGameOver_ShouldReturnNoneAndDropPlan()
    {
        var state = new GameState(GameConfig.Default) { Status = GameStatus.Running };
        var agent = NewAgent(state);
        agent.NextCommand(state);

        state.Status = GameStatus.GameOver;

        Assert.Equal(GameCommand.None, agent.NextCommand(state));
        Assert.Null(agent.Plan);
        Assert.Empty(agent.Candidates);
    }

    [Fact]
    public void NextCommand_WhenNoPlacementExists_ShouldHardDrop()
    {
        var state = new GameState(GameConfig.Default) { Status = GameStatus.Running };
        for (var x = 0; x < state.Width; x++)
        {
            for (var y = 0; y < state.Height; y++)
            {
                state.Board[x, y] = PieceKind.O;
            }
        }

        var agent = NewAgent(state);

        Assert.Equal(GameCommand.HardDrop, agent.NextCommand(state));
        Assert.Null(agent.Plan);
    }

    [Fact]
    public void NextCommand_ShouldNotMutateState()
    {
        var state = new GameState(GameConfig.Default) { Status = GameStatus.Running };
        var agent = NewAgent(state);
        var active = state.Active;
        var boardBefore = (PieceKind?[,])state.Board.Clone();

        for (var i = 0; i < 10; i++)
        {
            agent.NextCommand(state);
        }

        Assert.Same(active, state.Active);
        Assert.Equal(0, state.Frame);
        Assert.Equal(0, state.LinesCleared);
        for (var x = 0; x < state.Width; x++)
        {
            for (var y = 0; y < state.Height; y++)
            {
                Assert.Equal(boardBefore[x, y], state.Board[x, y]);
            }
        }
    }

    [Fact]
    public void AfterFirstDecision_ShouldExposeCandidatesAndChosen()
    {
        var state = new GameState(GameConfig.Default) { Status = GameStatus.Running };
        var agent = NewAgent(state);

        var command = agent.NextCommand(state);

        Assert.NotEqual(GameCommand.None, command);
        Assert.NotEmpty(agent.Candidates);
        Assert.NotNull(agent.Chosen);
        Assert.Contains(agent.Chosen!, agent.Candidates);
        Assert.Equal(agent.Chosen!.Rotation, agent.Plan!.Rotation);
        Assert.Equal(agent.Chosen.X, agent.Plan.X);
    }

    [Fact]
    public void Reset_ShouldClearPlanAndCandidates()
    {
        var state = new GameState(GameConfig.Default) { Status = GameStatus.Running };
        var agent = NewAgent(state);
        agent.NextCommand(state);

        agent.Reset();

        Assert.Empty(agent.Candidates);
        Assert.Null(agent.Chosen);
        Assert.Null(agent.Plan);
    }

    [Fact]
    public void Agent_ShouldClearAtLeastOneLine_WithinTwoThousandFrames()
    {
        var game = new SimulatedGame();
        var agent = new HeuristicAgent(game.State.Config, game.Rules);

        game.Run(agent, 2000);

        Assert.True(game.State.LinesCleared >= 1, $"2000 帧内一行都没消掉（消行 {game.State.LinesCleared}）");
    }

    [Fact]
    public void Agent_ShouldNeverThrow_OverALongGame()
    {
        var game = new SimulatedGame();
        var agent = new HeuristicAgent(game.State.Config, game.Rules);

        var exception = Record.Exception(() => game.Run(agent, 3000));

        Assert.Null(exception);
    }

    [Fact]
    public void Agent_ShouldKeepMakingProgressInsteadOfStalling()
    {
        var game = new SimulatedGame();
        var agent = new HeuristicAgent(game.State.Config, game.Rules);

        game.Run(agent, 2500);
        var firstWindow = game.State.LinesCleared;
        game.Run(agent, 2500);

        // 旧实现遇到"计划不可达"（挪不动、又转不了）会每 2 帧重规划一次、永远不硬降，
        // 棋局就卡在 Running 且一行都消不掉。修好后 AI 能一直活下去，所以判据从
        // "必须 GameOver" 改成"两个窗口之间必须持续消行"——这才真正在测"没卡死"。
        Assert.True(firstWindow > 0);
        Assert.True(
            game.State.LinesCleared - firstWindow >= 100,
            $"后 2500 帧只消了 {game.State.LinesCleared - firstWindow} 行");
    }

    [Fact]
    public void Agent_ShouldKeepRunningWithoutPreviewQueue()
    {
        var game = new SimulatedGame();
        var agent = new HeuristicAgent(game.State.Config, game.Rules);
        game.State.NextQueue.Clear();

        var exception = Record.Exception(() => game.Run(agent, 300));

        Assert.Null(exception);
        Assert.NotEqual(GameStatus.GameOver, game.State.Status);
    }

    [Fact]
    public void Agent_ShouldReachPlannedColumnAndRotationBeforeHardDrop()
    {
        var game = new SimulatedGame(sequence: new[] { PieceKind.O });
        var agent = new HeuristicAgent(game.State.Config, game.Rules);

        var frames = 0;
        AgentCandidate? planned = null;
        while (true)
        {
            planned = agent.Plan;
            var command = agent.NextCommand(game.State);
            if (command == GameCommand.HardDrop)
            {
                break;
            }

            game.Apply(command);
            frames++;
            Assert.True(frames < 30, "30 帧内没走到计划落点");
        }

        // 硬降成功后状态机会立刻清空计划，所以比对的是"发硬降之前"那个计划。
        Assert.NotNull(planned);
        Assert.Equal(planned!.X, game.State.Active.X);
        Assert.Equal(planned.Rotation, game.State.Active.Rotation);
    }
}
