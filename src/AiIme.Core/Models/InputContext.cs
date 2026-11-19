namespace AiIme.Core.Models;

/// <summary>
/// 当前编辑会话的上下文，供 n-gram / 上下文打分使用。
/// 第一版只保留最近已上屏的词序列，切换焦点时可重置。
/// </summary>
public sealed class InputContext
{
    /// <summary>最近若干个已上屏的词，按时间正序，末尾为最近一个。</summary>
    public IReadOnlyList<string> PreviousWords { get; init; } = Array.Empty<string>();

    /// <summary>本次输入会话内刚用过的词（用于会话内上浮）。</summary>
    public IReadOnlyList<string> RecentSessionWords { get; init; } = Array.Empty<string>();

    /// <summary>取上文最近 n 个词，超出长度时截断。</summary>
    public IReadOnlyList<string> Tail(int n)
    {
        if (PreviousWords.Count == 0) return Array.Empty<string>();
        var start = Math.Max(0, PreviousWords.Count - n);
        return PreviousWords.Skip(start).ToArray();
    }
}
