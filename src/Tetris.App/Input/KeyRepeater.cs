using Tetris.Core;

namespace Tetris.App.Input;

/// <summary>
/// 键盘连发器（B 负责）：把"物理按键"变成"这一帧要发几条 <see cref="GameCommand"/>"
/// （任务书 02 §五 DAS / ARR）。
/// <para>
/// 规则（与任务书一致）：
/// <list type="bullet">
/// <item>刚按下立刻发一条（<c>NextFireAt = 按下时刻</c>），延迟不超过一帧；</item>
/// <item>按住超过 <see cref="DasMs"/>（180ms）后进入连发，每 <see cref="ArrMs"/>（40ms）一条；</item>
/// <item>左右同按时<b>最后按下的方向优先</b>（方案 A）；被顶掉的方向退到排队状态，
/// 松手后 <see cref="ArrMs"/> 内接管，既不会卡顿也不会双倍速；</item>
/// <item>旋转、硬降、暂停这类<b>一次性</b>命令不进这里（由 <c>MainForm</c> 在按键当帧直接下发，按住不连发）；</item>
/// <item>松开立即停：<see cref="Release"/> 直接把按键移出表，不会有"惯性"。</item>
/// </list>
/// </para>
/// <para>
/// 计时全部来自调用方传入的单调节拍（<see cref="System.Diagnostics.Stopwatch"/> 的毫秒数），
/// 本类自己不读系统时间，因此可以在测试里用假时间精确验证 DAS/ARR 时序。
/// 暂停、失焦、最小化时由界面调用 <see cref="Clear"/>，恢复后不会补发一串积压的移动。
/// </para>
/// </summary>
internal sealed class KeyRepeater
{
    /// <summary>首次重复之前的等待（Delayed Auto Shift）。</summary>
    public const double DasMs = 180.0;

    /// <summary>进入连发后的每次间隔（Auto Repeat Rate）。</summary>
    public const double ArrMs = 40.0;

    /// <summary>
    /// 浮点比较的容差：Stopwatch 的毫秒是 double，帧边界上可能出现 179.9999 这种值，
    /// 不设容差会让 DAS 看起来偶尔"晚一帧"。
    /// </summary>
    private const double EpsilonMs = 0.0001;

    private readonly List<Hold> _holds = new(4);

    private long _sequence;

    /// <summary>某个按键当前的按住状态与下一次该发命令的时刻。</summary>
    private sealed class Hold
    {
        public required Keys Key { get; init; }

        /// <summary>DAS 结束时刻：第一次重复之后，下一次重复至少要等到这里。</summary>
        public required double DasEndAt { get; set; }

        /// <summary>下一次允许发命令的时刻。</summary>
        public required double NextFireAt { get; set; }

        /// <summary>按下顺序，越大越新（左右同按时用它选"最后按下的那个"）。</summary>
        public required long Sequence { get; init; }

        /// <summary>是否已经发过"按下当帧"的第一条。</summary>
        public bool Fired { get; set; }
    }

    /// <summary>该键现在是否被按住（界面用它吞掉 Windows 的自动重复）。</summary>
    public bool IsHeld(Keys key)
    {
        foreach (var hold in _holds)
        {
            if (hold.Key == key)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>当前按住的方向键数量（测试/调试用）。</summary>
    public int HeldCount => _holds.Count;

    /// <summary>
    /// 记录一次按下。<b>已经按住时返回 false</b>（那就是系统的自动重复消息，调用方应当吞掉），
    /// 第一次按下返回 true。
    /// </summary>
    /// <param name="key">物理键（不含修饰键）。</param>
    /// <param name="nowMs">单调节拍，毫秒。</param>
    public bool Press(Keys key, double nowMs)
    {
        if (IsHeld(key))
        {
            return false;
        }

        // 左右互斥（方案 A）：新的方向优先，另一个方向退到"排队"状态。
        // 退到排队的那一个不立刻发命令、也不重走一整套 DAS：
        // 它的下一次机会是 ArrMs 之后，这样"点一下反方向再松手"不会卡顿 180ms，
        // 也不会因为两个键交替按而跑出双倍速度（任何时候一帧最多一条横向命令）。
        if (key is Keys.Left or Keys.Right)
        {
            foreach (var hold in _holds)
            {
                if (hold.Key is Keys.Left or Keys.Right)
                {
                    hold.Fired = true;
                    hold.DasEndAt = nowMs + DasMs;
                    hold.NextFireAt = nowMs + ArrMs;
                }
            }
        }

        _holds.Add(new Hold
        {
            Key = key,
            DasEndAt = nowMs + DasMs,
            NextFireAt = nowMs,
            Sequence = ++_sequence,
        });

        return true;
    }

    /// <summary>松开：立即停止，不会有残留的连发。</summary>
    public void Release(Keys key)
    {
        _holds.RemoveAll(hold => hold.Key == key);
    }

    /// <summary>清空全部按键状态（暂停、失焦、最小化、开新局时调用）。</summary>
    public void Clear()
    {
        _holds.Clear();
        _sequence = 0;
    }

    /// <summary>
    /// 取这一帧该下发的命令：最多一条横向 + 一条软降，顺序固定（先横后竖），
    /// 由调用方在 <c>Tick()</c> 之前交给 <c>IGameEngine.Apply</c>。
    /// </summary>
    public IReadOnlyList<GameCommand> Collect(double nowMs)
    {
        var commands = new List<GameCommand>(2);
        EmitHorizontal(nowMs, commands);
        EmitSoftDrop(nowMs, commands);
        return commands;
    }

    /// <summary>横向只能有一条：最后按下的那个方向赢。</summary>
    private void EmitHorizontal(double nowMs, List<GameCommand> commands)
    {
        Hold? active = null;
        foreach (var hold in _holds)
        {
            if (hold.Key is not (Keys.Left or Keys.Right))
            {
                continue;
            }

            if (active is null || hold.Sequence > active.Sequence)
            {
                active = hold;
            }
        }

        if (active is null || nowMs + EpsilonMs < active.NextFireAt)
        {
            return;
        }

        commands.Add(active.Key == Keys.Left ? GameCommand.MoveLeft : GameCommand.MoveRight);
        Advance(active, nowMs);
    }

    /// <summary>软降按住同样连发（任务书只禁止旋转/硬降连发）。</summary>
    private void EmitSoftDrop(double nowMs, List<GameCommand> commands)
    {
        foreach (var hold in _holds)
        {
            if (hold.Key != Keys.Down)
            {
                continue;
            }

            if (nowMs + EpsilonMs < hold.NextFireAt)
            {
                return;
            }

            commands.Add(GameCommand.SoftDrop);
            Advance(hold, nowMs);
            return;
        }
    }

    /// <summary>
    /// 推进一步：第一次之后等到 DAS 结束，随后按 ARR 节拍；
    /// 如果已经落后（掉帧、Alt+Tab 回来），把下一次排到"现在之后"，避免一帧补发一大串。
    /// </summary>
    private static void Advance(Hold hold, double nowMs)
    {
        if (!hold.Fired)
        {
            hold.Fired = true;
            hold.NextFireAt = Math.Max(hold.DasEndAt, nowMs);
            return;
        }

        hold.NextFireAt += ArrMs;
        if (hold.NextFireAt < nowMs)
        {
            hold.NextFireAt = nowMs + ArrMs;
        }
    }
}
