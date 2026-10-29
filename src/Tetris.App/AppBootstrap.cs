using Tetris.Core;
using Tetris.Core.Engine;
using Tetris.Core.Rules;
using Tetris.Persistence;

namespace Tetris.App;

/// <summary>
/// 组装点。<b>全工程唯一需要"接线"的地方</b>：
/// 界面只认 <see cref="IGameEngine"/>，不知道真实引擎叫什么、怎么 new；
/// 规则只认 <see cref="IGameRules"/>，引擎和 AI 拿到的是同一套标准规则。
/// </summary>
internal static class AppBootstrap
{
    /// <summary>
    /// 标准规则工厂（任务书 02 §八）：规则是<b>无状态</b>的纯逻辑，可以放心地造多个实例，
    /// 每个实例只服务于"同一个使用方"，互不干扰。引擎和 F3 的 AI 各自持有一个实例，
    /// 二者算出来的碰撞/消除结果完全一致。
    /// </summary>
    public static IGameRules CreateRules() => new StandardGameRules();

    /// <summary>
    /// 创建真实引擎：A 的 <see cref="GameEngine"/> + 标准规则 + 可复现的随机数源。
    /// <paramref name="seed"/> 由界面传入（开局用 <see cref="Environment.TickCount"/>），
    /// 同一个 seed 必然得到同一串方块，便于复现问题。
    /// </summary>
    public static IGameEngine CreateEngine(int seed) =>
        new GameEngine(GameConfig.Default, CreateRules(), new XorShiftRandomSource(seed));

    public static IHighScoreStore CreateStore() => new XmlHighScoreStore(HighScorePath);

    /// <summary>存档路径放在用户漫游目录，避免程序装在 Program Files 时写不进去。</summary>
    public static string HighScorePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "TetrisCourse",
        "highscore.xml");
}
