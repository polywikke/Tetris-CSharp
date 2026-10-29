using Tetris.AI;
using Tetris.Core.Engine;
using Tetris.Core.Rules;

namespace Tetris.Core.Tests;

/// <summary>
/// 启发式 AI × 真引擎。其余 AI 用例都跑在 <c>FakeGameRules</c> 上，这里只回答一个问题：
/// 把 C 的 AI 接到 A 的真引擎上，它能不能真的玩、会不会改坏状态、会不会卡死。
/// 这些用例允许跑几千帧（百毫秒级），换取对整条链路的真实验证。
/// </summary>
public class HeuristicAgentIntegrationTests
{
    private static (GameEngine Engine, HeuristicAgent Agent) NewGame(int seed)
    {
        var rules = new StandardGameRules();
        var engine = new GameEngine(GameConfig.Default, rules, new XorShiftRandomSource(seed));
        engine.Start(seed);
        return (engine, new HeuristicAgent(GameConfig.Default, rules));
    }

    /// <summary>按 60Hz 的逻辑节奏跑若干帧：AI 出指令 → 引擎应用 → 引擎推进。</summary>
    private static void Play(GameEngine engine, HeuristicAgent agent, int frames)
    {
        for (var i = 0; i < frames && engine.State.Status == GameStatus.Running; i++)
        {
            engine.Apply(agent.NextCommand(engine.State));
            engine.Tick();
        }
    }

    private static void AssertSameBoard(GameState expected, GameState actual)
    {
        for (var x = 0; x < expected.Width; x++)
        {
            for (var y = 0; y < expected.Height; y++)
            {
                Assert.Equal(expected.Board[x, y], actual.Board[x, y]);
            }
        }
    }

    [Fact]
    public void Agent_OnRealEngine_ShouldClearLinesAndScore()
    {
        var (engine, agent) = NewGame(20261029);

        Play(engine, agent, 1200);

        Assert.True(engine.State.LinesCleared > 0, "1200 帧内应至少消掉一行");
        Assert.True(engine.State.Score > 0);
        Assert.NotNull(agent.Chosen);
    }

    [Fact]
    public void Agent_Decision_ShouldNotMutateEngineState()
    {
        var (engine, agent) = NewGame(7);
        Play(engine, agent, 120);
        var snapshot = engine.State.Clone();

        agent.NextCommand(engine.State);

        AssertSameBoard(snapshot, engine.State);
        Assert.Equal(snapshot.Score, engine.State.Score);
        Assert.Equal(snapshot.Active.X, engine.State.Active.X);
    }

    [Fact]
    public void Agent_LongRun_ShouldNeverThrow()
    {
        var (engine, agent) = NewGame(42);

        var error = Record.Exception(() => Play(engine, agent, 3000));

        Assert.Null(error);
        Assert.True(engine.State.Frame > 0);
    }

    [Fact]
    public void Agent_WhenPaused_ShouldNotMove()
    {
        var (engine, agent) = NewGame(9);
        engine.Apply(GameCommand.Pause);

        Assert.Equal(GameCommand.None, agent.NextCommand(engine.State));
    }

    [Fact]
    public void Agent_WhenSpawnIsBlocked_ShouldEndTheGame()
    {
        var (engine, agent) = NewGame(3);
        for (var y = 0; y < 6; y++)
        {
            for (var x = 0; x < engine.State.Width - 1; x++)
            {
                engine.State.Board[x, y] = PieceKind.I;
            }
        }

        Play(engine, agent, 300);

        Assert.Equal(GameStatus.GameOver, engine.State.Status);
    }
}
