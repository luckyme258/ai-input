namespace AiIme.Core.Composition;

/// <summary>
/// 把一串连续拼音字母（或含空格分段）切分为音节序列。
/// 纯托管、可单测；合法音节集合由词典在加载时喂入，避免在此硬编码全表。
///
/// 规则：
/// - 输入含空格时，直接按空格分段；
/// - 否则从左到右做最长匹配：在合法音节集合内取最长前缀；
/// - 匹配不到音节的残片作为未完成音节保留，交由索引做前缀兜底。
/// </summary>
public static class PinyinSegmenter
{
    /// <summary>音节最大长度（zhuang/chuang 等为 6）。</summary>
    private const int MaxSyllableLength = 6;

    /// <summary>
    /// 切分输入。返回的音节序列中，最后一个元素可能是尚未拼完的残片。
    /// </summary>
    public static IReadOnlyList<string> Segment(string input, ISet<string> syllables)
    {
        if (string.IsNullOrWhiteSpace(input)) return Array.Empty<string>();

        var normalized = input.Trim().ToLowerInvariant();
        if (normalized.Contains(' '))
        {
            return normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        var tokens = new List<string>();
        int i = 0;
        while (i < normalized.Length)
        {
            int matchLen = LongestMatch(normalized, i, syllables);
            if (matchLen > 0)
            {
                tokens.Add(normalized.Substring(i, matchLen));
                i += matchLen;
            }
            else
            {
                // 当前位置起不成音节：先取一个字母作为残片继续，保证后续仍能前缀匹配。
                tokens.Add(normalized[i].ToString());
                i += 1;
            }
        }

        return tokens;
    }

    private static int LongestMatch(string s, int start, ISet<string> syllables)
    {
        int max = Math.Min(MaxSyllableLength, s.Length - start);
        for (int len = max; len >= 2; len--)
        {
            if (syllables.Contains(s.Substring(start, len))) return len;
        }

        // 单元音 a/o/e 单独成音节；其它单字母不作为完整音节。
        if (max >= 1 && IsSingleVowel(s[start]) &&
            syllables.Contains(s[start].ToString()))
        {
            return 1;
        }

        return 0;
    }

    private static bool IsSingleVowel(char ch) => ch is 'a' or 'o' or 'e';
}
