using System.Xml;
using System.Xml.Serialization;
using Tetris.Core;

namespace Tetris.Persistence;

/// <summary>
/// 高分存档的 XML 实现（B 负责）。
/// <para>
/// 已经做到：缺文件返回 Missing、坏 XML 返回 Corrupted、读写异常返回 Unreadable/false，
/// 写入走"先写 .tmp 再原子替换"，避免写一半断电留下半个文件。
/// </para>
/// <para>
/// TODO(B)：以下场景仍需你亲手验证并补完，这也是评分里 15 分的盲测重点——
/// 目录只读、文件被别的进程独占、磁盘满、路径被换成目录、Save 失败后界面如何提示。
/// </para>
/// </summary>
public sealed class XmlHighScoreStore : IHighScoreStore
{
    private static readonly XmlSerializer Serializer = new(typeof(HighScoreDto));

    private readonly string _path;

    public XmlHighScoreStore(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _path = Path.GetFullPath(path);
    }

    public string Location => _path;

    public HighScoreLoadResult Load()
    {
        try
        {
            if (!File.Exists(_path))
            {
                return HighScoreLoadResult.Missing();
            }

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
            // XmlSerializer 把格式错误包成 InvalidOperationException。
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
        ArgumentNullException.ThrowIfNull(record);

        var tempPath = _path + ".tmp";
        try
        {
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

            using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                Serializer.Serialize(stream, dto);
            }

            // 同一卷内的 Move(overwrite) 等价于替换，避免出现"写了一半的存档"。
            File.Move(tempPath, _path, overwrite: true);
            return true;
        }
        catch (Exception ex)
        {
            _ = ex;
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
