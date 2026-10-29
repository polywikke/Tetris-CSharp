using System.Xml;
using System.Xml.Serialization;
using Tetris.Core;

namespace Tetris.Persistence;

/// <summary>
/// 高分存档的 XML 实现（B 负责）。契约：<see cref="Load"/> 与 <see cref="Save"/> <b>永不抛异常</b>，
/// 所有失败都翻译成 <see cref="HighScoreLoadStatus"/> 或 <c>false</c>，界面因此永远不需要 try/catch。
/// <para>
/// 覆盖任务书 02 §六 的四个盲测场景：
/// <list type="number">
/// <item>文件不存在 → <see cref="HighScoreLoadStatus.Missing"/>，界面按"首次运行、0 分"正常开局；</item>
/// <item>文件里是乱码 / 根节点不对 / 负数 → <see cref="HighScoreLoadStatus.Corrupted"/>，忽略旧档；</item>
/// <item>文件只读 → <see cref="Save"/> 返回 <c>false</c>（游戏继续，界面提示未保存）；</item>
/// <item>文件被别的进程独占 → <see cref="HighScoreLoadStatus.Unreadable"/>，不崩溃也不假死。</item>
/// </list>
/// 另外还额外兜住了几个"顺手就会踩到"的情况：路径被换成目录、磁盘满/无写权限、
/// 以及存档文件异常巨大（明显不是我们写的，直接判 Corrupted，避免反序列化卡住界面）。
/// </para>
/// <para>
/// 写入一律走"先写同目录下的 <c>.tmp</c>，再 <see cref="File.Move(string, string, bool)"/> 原子替换"，
/// 因此任何时刻崩溃/断电，正式存档要么是旧内容、要么是新内容，不会留下半个文件。
/// </para>
/// </summary>
public sealed class XmlHighScoreStore : IHighScoreStore
{
    /// <summary>
    /// 存档文件大小上限。正常存档不到 1KB，超过这个数几乎可以肯定是别的东西写坏了；
    /// 直接判 Corrupted，避免反序列化一个几十 MB 的文件让界面卡住（盲测的"不假死"）。
    /// </summary>
    public const long MaxFileBytes = 64 * 1024;

    private static readonly XmlSerializer Serializer = new(typeof(HighScoreDto));

    private readonly string _path;

    public XmlHighScoreStore(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _path = Path.GetFullPath(path);
    }

    /// <summary>正式存档的绝对路径（临时文件是它 + <c>.tmp</c>）。</summary>
    public string Location => _path;

    /// <summary>最近一次 <see cref="Save"/> 失败的原因；成功时清空。仅用于排查，界面可以不看。</summary>
    public string? LastSaveError { get; private set; }

    public HighScoreLoadResult Load()
    {
        try
        {
            // 被人手滑把路径换成了同名目录：既不是"没有存档"，也不该当成空档去覆盖。
            if (Directory.Exists(_path))
            {
                return HighScoreLoadResult.Unreadable("存档路径是一个目录");
            }

            if (!File.Exists(_path))
            {
                return HighScoreLoadResult.Missing();
            }

            if (new FileInfo(_path).Length > MaxFileBytes)
            {
                return HighScoreLoadResult.Corrupted($"文件超过 {MaxFileBytes} 字节，不是本程序写的");
            }

            // FileShare.Read：别的进程正独占这个文件时这里会抛 IOException，被下面兜成 Unreadable。
            using var stream = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.Read);

            if (Serializer.Deserialize(stream) is not HighScoreDto dto)
            {
                return HighScoreLoadResult.Corrupted("XML 根节点不是 HighScore");
            }

            if (dto.Score < 0 || dto.Level < 0 || dto.Lines < 0)
            {
                return HighScoreLoadResult.Corrupted("分数/等级/消行不能为负");
            }

            return HighScoreLoadResult.Ok(new HighScore(dto.Score, dto.Level, dto.Lines, dto.AchievedAt));
        }
        catch (InvalidOperationException ex)
        {
            // XmlSerializer 把"格式错误 / 根节点不对 / 数值不是数字"都包成 InvalidOperationException。
            return HighScoreLoadResult.Corrupted(Describe(ex));
        }
        catch (XmlException ex)
        {
            return HighScoreLoadResult.Corrupted(Describe(ex));
        }
        catch (Exception ex)
        {
            // 被占用、无权限、路径非法……统一按"读不了"处理，绝不让异常冒到界面。
            return HighScoreLoadResult.Unreadable(Describe(ex));
        }
    }

    public bool Save(HighScore record)
    {
        LastSaveError = null;

        // 契约要求不抛：连参数为 null 也只返回 false。
        if (record is null)
        {
            LastSaveError = "记录为 null";
            return false;
        }

        var tempPath = _path + ".tmp";
        try
        {
            if (Directory.Exists(_path))
            {
                LastSaveError = "存档路径是一个目录";
                return false;
            }

            var directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var dto = new HighScoreDto
            {
                Score = record.Score,
                Level = record.Level,
                Lines = record.Lines,
                AchievedAt = record.AchievedAt,
            };

            // 1) 先写临时文件；Flush(true) 把内容刷到磁盘，避免"改名成功但内容是空的"。
            using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                Serializer.Serialize(stream, dto);
                stream.Flush(flushToDisk: true);
            }

            // 2) 同一目录内的 Move(overwrite) 等价于原子替换。
            //    目标只读 / 被独占 / 权限不足时这里会抛异常，被下面兜成 false。
            File.Move(tempPath, _path, overwrite: true);
            return true;
        }
        catch (Exception ex)
        {
            LastSaveError = Describe(ex);
            TryDelete(tempPath);
            return false;
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // 清理临时文件失败不影响结果。
        }
    }

    private static string Describe(Exception ex)
    {
        var inner = ex.InnerException ?? ex;
        return $"{inner.GetType().Name}: {inner.Message}";
    }
}
