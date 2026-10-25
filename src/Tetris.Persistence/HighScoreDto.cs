using System.Xml.Serialization;

namespace Tetris.Persistence;

/// <summary>
/// XML 映射对象。<b>只有它</b>允许长成这样（公开无参构造 + 可写属性），
/// 不要把它当业务模型传给界面或 AI。
/// </summary>
[XmlRoot("HighScore")]
public sealed class HighScoreDto
{
    public int Score { get; set; }

    public int Level { get; set; }

    public int Lines { get; set; }

    public DateTimeOffset AchievedAt { get; set; }
}
