using Tetris.Core;

namespace Tetris.Core.Tests.Fakes;

/// <summary>
/// 迷你引擎：把 <see cref="FakeGameRules"/> 与 <see cref="GameState"/> 拼起来，逐帧应用 <see cref="GameCommand"/>，
/// 硬降后锁定并生成下一块。只为测试 AI 而存在，不参与产品运行。
/// </summary>
public sealed class SimulatedGame
{
    private static readonly PieceKind[] DefaultSequence =
    {
        PieceKind.I, PieceKind.O, PieceKind.T, PieceKind.S, PieceKind.Z, PieceKind.J, PieceKind.L,
    };

    private readonly PieceKind[] _sequence;
    private int _sequenceIndex;

    /// <param name="config">棋盘配置，默认 12×16。</param>
    /// <param name="sequence">方块出场顺序；省略时按 I,O,T,S,Z,J,L 循环。</param>
    /// <param name="rules">规则实现；默认用 <see cref="FakeGameRules"/>，可换成 <c>StandardGameRules</c>。</param>
    public SimulatedGame(GameConfig? config = null, IEnumerable<PieceKind>? sequence = null, IGameRules? rules = null)
    {
        State = new GameState(config ?? GameConfig.Default);
        Rules = rules ?? new FakeGameRules();
        _sequence = sequence?.ToArray() ?? DefaultSequence;
        if (_sequence.Length == 0)
        {
            _sequence = DefaultSequence;
        }

        for (var i = 0; i < State.Config.PreviewCount; i++)
        {
            State.NextQueue.Enqueue(NextKind());
        }

        State.Status = GameStatus.Running;
        SpawnNext();
    }

    public GameState State { get; }

    public IGameRules Rules { get; }

    /// <summary>应用一条指令；返回它是否真的改变了状态。</summary>
    public bool Apply(GameCommand command)
    {
        if (State.Status != GameStatus.Running)
        {
            return false;
        }

        State.Frame++;
        switch (command)
        {
            case GameCommand.MoveLeft:
                return Rules.TryMove(State, -1);
            case GameCommand.MoveRight:
                return Rules.TryMove(State, 1);
            case GameCommand.SoftDrop:
                return TryShiftDown();
            case GameCommand.RotateCw:
                return Rules.TryRotate(State, 1);
            case GameCommand.RotateCcw:
                return Rules.TryRotate(State, -1);
            case GameCommand.HardDrop:
                HardDrop();
                return true;
            case GameCommand.Pause:
                State.Status = GameStatus.Paused;
                return true;
            case GameCommand.Resume:
                if (State.Status == GameStatus.Paused)
                {
                    State.Status = GameStatus.Running;
                    return true;
                }

                return false;
            default:
                return false;
        }
    }

    /// <summary>让自动玩家连续决策若干帧。</summary>
    public void Run(IPlayerAgent agent, int frames)
    {
        ArgumentNullException.ThrowIfNull(agent);
        for (var i = 0; i < frames && State.Status == GameStatus.Running; i++)
        {
            Apply(agent.NextCommand(State));
        }
    }

    /// <summary>硬降 + 锁定 + 生成下一块（与 A 的引擎语义一致，缺的只是锁定延迟和出块动画）。</summary>
    public void HardDrop()
    {
        var piece = State.Active;
        State.Active = piece.With(y: piece.Y + Rules.DropDistance(State, piece));
        Rules.LockActive(State);
        SpawnNext();
    }

    private bool TryShiftDown()
    {
        var moved = State.Active.With(y: State.Active.Y + 1);
        if (Rules.Collides(State, moved))
        {
            return false;
        }

        State.Active = moved;
        return true;
    }

    private void SpawnNext()
    {
        var kind = State.NextQueue.Count > 0 ? State.NextQueue.Dequeue() : NextKind();
        State.NextQueue.Enqueue(NextKind());

        var piece = new Tetromino(kind, State.ShapeOf(kind), State.SpawnX(), 0);
        if (Rules.Collides(State, piece))
        {
            State.Status = GameStatus.GameOver;
            return;
        }

        State.Active = piece;
    }

    private PieceKind NextKind()
    {
        var kind = _sequence[_sequenceIndex % _sequence.Length];
        _sequenceIndex++;
        return kind;
    }
}
