namespace Tetris.Core;

/// <summary>需求规定的计分：一次消除 k 行得 100 × k 分。等级不参与计算。</summary>
public sealed class StandardScoreRule : IScoreRule
{
    public int ScoreFor(int linesCleared, int level)
    {
        if (linesCleared <= 0)
        {
            return 0;
        }

        return 100 * linesCleared;
    }
}
