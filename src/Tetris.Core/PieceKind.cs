namespace Tetris.Core;

/// <summary>
/// 七种标准方块。形状数据在 <see cref="PieceShapes"/> / <see cref="GameConfig.Shapes"/> 中，
/// 新增一种方块意味着同时扩展本枚举（属核心契约变更，见 docs/分工与接口契约.md §6）。
/// </summary>
public enum PieceKind
{
    I,
    O,
    T,
    S,
    Z,
    J,
    L,
}
