namespace Tetris.Core.Testing;

/// <summary>
/// 假引擎：<b>只是界面开发的脚手架</b>，把 A 的 <see cref="IGameEngine"/> 顶掉，
/// 让 B 在 D1 就能跑界面、试按键、调布局。
/// <para>
/// 它只做"移动/旋转/下落"这几个看得见的动作，<b>不消行、不计分、不生成新方块</b>，
/// 所以它不是产品代码。A 交付真引擎后，B 只需改 <c>AppBootstrap.CreateEngine</c> 一行并删除本文件。
/// </para>
/// </summary>
public sealed class DemoEngine : IGameEngine
{
    private readonly GameState _state;

    public DemoEngine(GameConfig? config = null)
    {
        _state = DemoStates.Create(config);
    }

    public GameState State => _state;

    public void Start(int seed)
    {
        // 假引擎不重建状态；真实引擎在这里清盘并重建方块序列。
    }

    public void Tick() => _state.Frame++;

    public bool Apply(GameCommand command)
    {
        if (_state.Status != GameStatus.Running)
        {
            return false;
        }

        switch (command)
        {
            case GameCommand.MoveLeft:
                return TryShift(-1, 0);
            case GameCommand.MoveRight:
                return TryShift(1, 0);
            case GameCommand.SoftDrop:
                return TryShift(0, 1);
            case GameCommand.RotateCw:
                return TryRotate(1);
            case GameCommand.RotateCcw:
                return TryRotate(-1);
            case GameCommand.HardDrop:
                while (TryShift(0, 1))
                {
                }

                return true;
            case GameCommand.Pause:
                _state.Status = GameStatus.Paused;
                return true;
            case GameCommand.Resume:
                if (_state.Status == GameStatus.Paused)
                {
                    _state.Status = GameStatus.Running;
                    return true;
                }

                return false;
            default:
                return false;
        }
    }

    private bool TryShift(int dx, int dy)
    {
        var moved = _state.Active.With(_state.Active.X + dx, _state.Active.Y + dy);
        if (Collides(moved))
        {
            return false;
        }

        _state.Active = moved;
        return true;
    }

    private bool TryRotate(int direction)
    {
        var rotated = _state.Active.With(rotation: _state.Active.Rotation + direction);
        if (Collides(rotated))
        {
            return false;
        }

        _state.Active = rotated;
        return true;
    }

    private bool Collides(Tetromino piece)
    {
        foreach (var cell in piece.Cells)
        {
            if (cell.X < 0 || cell.X >= _state.Width || cell.Y >= _state.Height)
            {
                return true;
            }

            if (cell.Y >= 0 && _state.Board[cell.X, cell.Y].HasValue)
            {
                return true;
            }
        }

        return false;
    }
}
