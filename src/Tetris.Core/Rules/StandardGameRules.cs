namespace Tetris.Core.Rules;

/// <summary>
/// 标准俄罗斯方块规则（A 负责）：碰撞、下落距离、左右移动、旋转踢墙、锁定消行。
/// <para>
/// 它是<b>纯逻辑</b>：不依赖时间、随机数与界面，不抛异常，同样输入必然得到同样输出；
/// 只读写传入的 <see cref="GameState"/>，不修改 <see cref="GameConfig"/> 与 <see cref="PieceShape"/>。
/// C 的 AI 在副本上推演时可以直接复用同一个实例。
/// </para>
/// <para>
/// 交付进度：本次完成 ① 规则三件套（<see cref="Collides"/>、<see cref="DropDistance"/>、
/// <see cref="TryMove"/>）。② 旋转踢墙与 ③ 锁定消行按任务书顺序后续补在本类中，
/// 在补齐之前这两个方法会抛 <see cref="NotImplementedException"/>，避免"静默返回错误结果"。
/// </para>
/// </summary>
public sealed class StandardGameRules : IGameRules
{
    /// <summary>包围盒边长，与 <see cref="PieceShape.BoxSize"/> 保持一致，用于下落距离的防御性上界。</summary>
    private const int BoxSize = PieceShape.BoxSize;

    /// <summary>
    /// 该姿态是否与墙、地或已固定方块重叠。<b>纯查询</b>，不改变任何状态。
    /// </summary>
    /// <remarks>
    /// 坐标约定 <c>[x, y]</c>：x 是列、y 是行、<c>y = 0</c> 在顶部。判定顺序与任务书 5.2 一致：
    /// <list type="number">
    /// <item>方块格 <c>x &lt; 0</c> 或 <c>x &gt;= Width</c> —— 碰到左右墙；</item>
    /// <item>方块格 <c>y &gt;= Height</c> —— 碰到地板；</item>
    /// <item>方块格 <c>y &gt;= 0</c> 且 <c>Board[x, y]</c> 已被占据 —— 叠在别的方块上；</item>
    /// <item><c>y &lt; 0</c> —— 允许探出棋盘顶部，忽略。</item>
    /// </list>
    /// 传入 null 时返回 <c>true</c>（视为非法姿态），保证方法永不抛异常。
    /// </remarks>
    public bool Collides(GameState state, Tetromino piece)
    {
        if (state is null || piece is null)
        {
            return true;
        }

        foreach (var cell in piece.Cells)
        {
            if (cell.X < 0 || cell.X >= state.Width)
            {
                return true;
            }

            if (cell.Y >= state.Height)
            {
                return true;
            }

            // y < 0 的格子在棋盘外，允许存在；只有落到棋盘内才查占用。
            if (cell.Y >= 0 && state.Board[cell.X, cell.Y].HasValue)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 方块从当前位置竖直下落还能掉几格（0 表示已经贴底或已经重叠）。<b>纯查询</b>，不改变状态。
    /// </summary>
    /// <remarks>
    /// 逐格向后试探，直到下一个位置会碰撞为止；循环上限取 <c>Height + BoxSize</c>，
    /// 正常局面（生成时 <c>Y = 0</c>）远达不到，只是为了防御异常输入导致死循环。
    /// </remarks>
    public int DropDistance(GameState state, Tetromino piece)
    {
        if (state is null || piece is null)
        {
            return 0;
        }

        var distance = 0;
        var limit = state.Height + BoxSize;
        for (var step = 1; step <= limit; step++)
        {
            if (Collides(state, piece.With(y: piece.Y + step)))
            {
                break;
            }

            distance = step;
        }

        return distance;
    }

    /// <summary>
    /// 把当前方块左右移动 <paramref name="deltaX"/> 格。
    /// 被墙或已固定方块挡住时返回 <c>false</c>，且<b>状态完全不变</b>。
    /// </summary>
    /// <remarks>
    /// 只操作 <see cref="GameState.Active"/>：越界判断交给 <see cref="Collides"/>，
    /// 因此 <c>deltaX</c> 为任意整数都安全，<c>deltaX = 0</c> 时返回 <c>true</c> 且不产生可见变化。
    /// </remarks>
    public bool TryMove(GameState state, int deltaX)
    {
        if (state is null)
        {
            return false;
        }

        var moved = state.Active.With(x: state.Active.X + deltaX);
        if (Collides(state, moved))
        {
            return false;
        }

        state.Active = moved;
        return true;
    }

    /// <summary>TODO(②)：旋转（+1 顺时针 / -1 逆时针）与简化 SRS 踢墙，尚未实现。</summary>
    /// <exception cref="NotImplementedException">交付 ② 之前调用会抛出，避免静默返回错误结果。</exception>
    public bool TryRotate(GameState state, int direction) =>
        throw new NotImplementedException("TODO(②)：旋转与踢墙尚未实现（A 的交付 ②）。");

    /// <summary>TODO(③)：锁定当前方块 → 消行 → 计分 → 升级，尚未实现。</summary>
    /// <exception cref="NotImplementedException">交付 ③ 之前调用会抛出，避免静默返回错误结果。</exception>
    public int LockActive(GameState state) =>
        throw new NotImplementedException("TODO(③)：锁定、消行与计分尚未实现（A 的交付 ③）。");
}
