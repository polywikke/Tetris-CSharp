using Tetris.Core;

namespace Tetris.Core.Tests.Fakes;

/// <summary>
/// A 的 <see cref="IGameRules"/> 交付之前的测试替身：只实现最基本的下落/碰撞/消行，
/// 行为与 docs/01 的规则描述一致（重力下落、满行消除、按 <see cref="IScoreRule"/> 计分），
/// 但踢墙只做最简单的左右偏移，且不做"锁定延迟"等引擎细节。
/// <para>它让 C 的 AI 测试不必等 A 的规则实现就能跑，A 交付后把注入点换成真规则即可。</para>
/// </summary>
public sealed class FakeGameRules : IGameRules
{
    /// <summary>旋转转不动时依次尝试的水平偏移。</summary>
    private static readonly int[] KickOffsets = { 0, -1, 1, -2, 2 };

    public bool Collides(GameState state, Tetromino piece)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(piece);

        foreach (var cell in piece.Cells)
        {
            if (cell.X < 0 || cell.X >= state.Width || cell.Y >= state.Height)
            {
                return true;
            }

            if (cell.Y >= 0 && state.Board[cell.X, cell.Y].HasValue)
            {
                return true;
            }
        }

        return false;
    }

    public int DropDistance(GameState state, Tetromino piece)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(piece);

        var distance = 0;
        while (!Collides(state, piece.With(y: piece.Y + distance + 1)))
        {
            distance++;
        }

        return distance;
    }

    public bool TryMove(GameState state, int deltaX)
    {
        ArgumentNullException.ThrowIfNull(state);

        var moved = state.Active.With(x: state.Active.X + deltaX);
        if (Collides(state, moved))
        {
            return false;
        }

        state.Active = moved;
        return true;
    }

    public bool TryRotate(GameState state, int direction)
    {
        ArgumentNullException.ThrowIfNull(state);

        var rotated = state.Active.With(rotation: state.Active.Rotation + direction);
        foreach (var offset in KickOffsets)
        {
            var candidate = rotated.With(x: rotated.X + offset);
            if (!Collides(state, candidate))
            {
                state.Active = candidate;
                return true;
            }
        }

        return false;
    }

    public int LockActive(GameState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var active = state.Active;
        foreach (var cell in active.Cells)
        {
            if (cell.X >= 0 && cell.X < state.Width && cell.Y >= 0 && cell.Y < state.Height)
            {
                state.Board[cell.X, cell.Y] = active.Kind;
            }
        }

        var fullRows = new List<int>();
        for (var y = 0; y < state.Height; y++)
        {
            if (state.IsRowFull(y))
            {
                fullRows.Add(y);
            }
        }

        if (fullRows.Count > 0)
        {
            // 从下往上把没有被消掉的行搬到目标位置，顶部空出来的行清空。
            var destination = state.Height - 1;
            for (var source = state.Height - 1; source >= 0; source--)
            {
                if (fullRows.Contains(source))
                {
                    continue;
                }

                if (destination != source)
                {
                    for (var x = 0; x < state.Width; x++)
                    {
                        state.Board[x, destination] = state.Board[x, source];
                    }
                }

                destination--;
            }

            for (; destination >= 0; destination--)
            {
                for (var x = 0; x < state.Width; x++)
                {
                    state.Board[x, destination] = null;
                }
            }
        }

        state.LinesCleared += fullRows.Count;
        state.Score += state.Config.ScoreRule.ScoreFor(fullRows.Count, state.Level);
        state.Level = 1 + (state.LinesCleared / Math.Max(1, state.Config.LevelUpLines));
        return fullRows.Count;
    }
}
