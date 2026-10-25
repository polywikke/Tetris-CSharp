namespace Tetris.Core.Testing;

/// <summary>
/// 造几个"看起来像在玩"的状态，供 B 在 A 的引擎交付前开发界面、供 C 写测试夹具。
/// <b>不属于产品功能</b>，运行时的真实状态只能由 <see cref="IGameEngine"/> 产生。
/// </summary>
public static class DemoStates
{
    public static GameState Create(GameConfig? config = null)
    {
        config ??= GameConfig.Default;
        var state = new GameState(config)
        {
            Status = GameStatus.Running,
            Score = 0x2A,
            Level = 2,
            LinesCleared = 7,
            Frame = 123,
        };

        // 底部三行做一个高低不平的堆叠，方便界面检查颜色、网格与边界。
        var palette = new[]
        {
            PieceKind.I, PieceKind.O, PieceKind.T, PieceKind.S,
            PieceKind.Z, PieceKind.J, PieceKind.L,
        };
        for (var y = state.Height - 3; y < state.Height; y++)
        {
            for (var x = 0; x < state.Width; x++)
            {
                if ((x + y) % 7 == 0)
                {
                    continue;
                }

                state.Board[x, y] = palette[(x + 2 * y) % palette.Length];
            }
        }

        state.Active = new Tetromino(PieceKind.T, config.Shapes[PieceKind.T], state.SpawnX(), 0);
        state.NextQueue.Enqueue(PieceKind.I);
        state.NextQueue.Enqueue(PieceKind.O);
        state.NextQueue.Enqueue(PieceKind.S);
        return state;
    }
}
