namespace Tetris.Core;

/// <summary>
/// 随机数来源。7-bag 洗牌用它，种子由 <see cref="IGameEngine.Start"/> 传入。
/// 用接口是为了让测试和回放能替换成确定序列。
/// </summary>
public interface IRandomSource
{
    /// <summary>返回 [0, maxExclusive) 的整数；maxExclusive &lt;= 0 时返回 0。</summary>
    int Next(int maxExclusive);
}

/// <summary>
/// xorshift32 实现，<b>同一 seed 在任何机器/任何 .NET 版本上序列完全一致</b>
/// （<see cref="Random"/> 不保证跨版本一致，所以回放/测试不要用它）。
/// </summary>
public sealed class XorShiftRandomSource : IRandomSource
{
    private uint _state;

    public XorShiftRandomSource(int seed)
    {
        _state = seed == 0 ? 2463534242u : unchecked((uint)seed);
    }

    public int Next(int maxExclusive)
    {
        if (maxExclusive <= 0)
        {
            return 0;
        }

        _state ^= _state << 13;
        _state ^= _state >> 17;
        _state ^= _state << 5;
        return (int)(_state % (uint)maxExclusive);
    }
}
