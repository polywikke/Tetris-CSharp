namespace Tetris.Core.Engine;

/// <summary>
/// 7-bag 方块序列：一袋装 7 种方块各一个，用 Fisher-Yates 洗牌后逐个取出，取空再装下一袋。
/// 它保证任意连续 7 个方块恰好包含 7 种各一次，避免"连着来五个 S"这种没法玩的序列。
/// <para>
/// 随机数来自构造时注入的 <see cref="IRandomSource"/>：<see cref="IGameEngine.Start"/> 先
/// <see cref="IRandomSource.Reseed"/> 再 <see cref="Clear"/>，所以同一种子在任何机器上都会
/// 产生完全相同的方块序列——这是回放、测试和"两次开局顺序一致"自测项的基础。
/// </para>
/// <para>
/// 洗牌只依赖 <see cref="IRandomSource.Next"/>，不碰 <see cref="Random"/>，也不依赖时间。
/// </para>
/// </summary>
public sealed class SevenBag
{
    /// <summary>一袋的固定内容；这里的顺序只影响可读性，洗牌会把它打乱。</summary>
    private static readonly PieceKind[] BagContent =
    {
        PieceKind.I, PieceKind.O, PieceKind.T, PieceKind.S, PieceKind.Z, PieceKind.J, PieceKind.L,
    };

    private readonly IRandomSource _random;
    private readonly Queue<PieceKind> _pieces = new();

    public SevenBag(IRandomSource random)
    {
        _random = random ?? throw new ArgumentNullException(nameof(random));
    }

    /// <summary>当前袋里还剩几个（调试/测试用）。</summary>
    public int Remaining => _pieces.Count;

    /// <summary>丢弃当前袋里剩下的方块。开新局时与 <see cref="IRandomSource.Reseed"/> 配对使用。</summary>
    public void Clear() => _pieces.Clear();

    /// <summary>取下一个方块；袋空了会先重新装袋并洗牌。</summary>
    public PieceKind Next()
    {
        if (_pieces.Count == 0)
        {
            Refill();
        }

        return _pieces.Dequeue();
    }

    /// <summary>装一袋：拷贝固定内容后做 Fisher-Yates（从后往前，每次与 [0, i] 中随机一个交换）。</summary>
    private void Refill()
    {
        var bag = (PieceKind[])BagContent.Clone();

        for (var i = bag.Length - 1; i > 0; i--)
        {
            var j = _random.Next(i + 1);
            (bag[i], bag[j]) = (bag[j], bag[i]);
        }

        foreach (var kind in bag)
        {
            _pieces.Enqueue(kind);
        }
    }
}
