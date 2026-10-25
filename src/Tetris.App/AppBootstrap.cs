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
    /// 当前使用假引擎，界面可以先跑起来。
    /// <para>
    /// 集成时（见 docs/00-项目总览与集成.md）把方法体换成：
    /// <code>
    /// var rules = new StandardGameRules();
    /// return new GameEngine(GameConfig.Default, rules, new XorShiftRandomSource(seed));
    /// </code>
    /// 需要 <c>using Tetris.Core.Engine;</c> 与 <c>using Tetris.Core.Rules;</c>。
    /// </para>
    /// </summary>
    public static IGameEngine CreateEngine(int seed) => new DemoEngine(GameConfig.Default);

    public static IHighScoreStore CreateStore() => new XmlHighScoreStore(HighScorePath);

    /// <summary>存档路径放在用户漫游目录，避免程序装在 Program Files 时写不进去。</summary>
    public static string HighScorePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "TetrisCourse",
        "highscore.xml");
}
