namespace Tetris.Core;

/// <summary>一条历史最高分记录（XML 存档的载荷）。</summary>
public sealed record HighScore(int Score, int Level, int Lines, DateTimeOffset AchievedAt);

public enum HighScoreLoadStatus
{
    /// <summary>读取成功。</summary>
    Ok,

    /// <summary>文件不存在（首次运行，属正常）。</summary>
    Missing,

    /// <summary>文件存在但不是合法 XML / 字段非法。</summary>
    Corrupted,

    /// <summary>文件存在但读不了（被占用、无权限、路径非法等）。</summary>
    Unreadable,
}

/// <summary>读取结果。因为"读不到"是正常业务场景，所以不用异常表达。</summary>
public sealed record HighScoreLoadResult(HighScoreLoadStatus Status, HighScore? Record, string? Detail = null)
{
    public static HighScoreLoadResult Ok(HighScore record) => new(HighScoreLoadStatus.Ok, record);

    public static HighScoreLoadResult Missing() => new(HighScoreLoadStatus.Missing, null);

    public static HighScoreLoadResult Corrupted(string detail) => new(HighScoreLoadStatus.Corrupted, null, detail);

    public static HighScoreLoadResult Unreadable(string detail) => new(HighScoreLoadStatus.Unreadable, null, detail);
}

/// <summary>
/// 高分持久化（B 实现，B 使用；C 的测试可以用假实现替换）。
/// 约定：<b>Load/Save 都不抛异常</b>——存档缺失、损坏、被占用、目录只读都必须优雅返回。
/// </summary>
public interface IHighScoreStore
{
    /// <summary>存档文件完整路径，用于 README、报告和演示时手动删文件。</summary>
    string Location { get; }

    HighScoreLoadResult Load();

    /// <summary>写入成功返回 true；任何失败都返回 false，由界面决定提示方式。</summary>
    bool Save(HighScore record);
}
