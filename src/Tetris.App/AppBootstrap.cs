using Tetris.Core;
using Tetris.Core.Testing;
using Tetris.Persistence;

namespace Tetris.App;

/// <summary>
/// 组装点。<b>全工程唯一需要"接线"的地方</b>：
/// 界面只认 <see cref="IGameEngine"/>，不知道真实引擎叫什么、怎么 new。
/// </summary>
internal static class AppBootstrap
{
    /// <summary>
    /// TODO(B)：A 交付 <c>GameEngine</c> 后，把下面一行换成
    /// <code>new Tetris.Core.Engine.GameEngine(GameConfig.Default, new StandardGameRules(), new XorShiftRandomSource(seed))</code>
    /// 然后删除对 DemoEngine 的 using。
    /// </summary>
    public static IGameEngine CreateEngine(int seed) => new DemoEngine(GameConfig.Default);

    public static IHighScoreStore CreateStore() => new XmlHighScoreStore(HighScorePath);

    /// <summary>存档路径放在用户漫游目录，避免程序装在 Program Files 时写不进去。</summary>
    public static string HighScorePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "TetrisCourse",
        "highscore.xml");
}
