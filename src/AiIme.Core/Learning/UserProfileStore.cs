namespace AiIme.Core.Learning;

/// <summary>
/// 用户习惯记忆的持久化接口。第一版用 SQLite 实现；
/// 该接口把存储与学习算法解耦，便于单测与后续替换。
/// </summary>
public interface IUserProfileStore
{
    /// <summary>读取某词的个人累计频次（未记录返回 0）。</summary>
    double GetWordFrequency(string word);

    /// <summary>批量读取一组词的个人频次；未记录的词不出现在结果中。默认实现逐条查询。</summary>
    IReadOnlyDictionary<string, double> GetWordFrequencies(IEnumerable<string> words)
    {
        var dict = new Dictionary<string, double>();
        foreach (var word in words.Distinct())
        {
            if (!string.IsNullOrEmpty(word)) dict[word] = GetWordFrequency(word);
        }
        return dict;
    }

    /// <summary>累加一个词的频次；<paramref name="weight"/> 用于纠正加权等场景。</summary>
    void AddWordFrequency(string word, double weight = 1.0);

    /// <summary>读取二元搭配计数：上文词 -> 下文词。</summary>
    long GetBigramCount(string previous, string next);

    /// <summary>批量读取同一上文词下一组下文词的二元计数；默认实现逐条查询。</summary>
    IReadOnlyDictionary<string, long> GetBigramCounts(string previous, IEnumerable<string> nexts)
    {
        var dict = new Dictionary<string, long>();
        foreach (var next in nexts.Distinct())
        {
            if (!string.IsNullOrEmpty(next)) dict[next] = GetBigramCount(previous, next);
        }
        return dict;
    }

    /// <summary>记录一次二元搭配。</summary>
    void AddBigram(string previous, string next);

    /// <summary>读取三元搭配计数：(w1,w2) -> next。</summary>
    long GetTrigramCount(string w1, string w2, string next);

    /// <summary>批量读取同一上文 (w1,w2) 下一组下文词的三元计数；默认实现逐条查询。</summary>
    IReadOnlyDictionary<string, long> GetTrigramCounts(string w1, string w2, IEnumerable<string> nexts)
    {
        var dict = new Dictionary<string, long>();
        foreach (var next in nexts.Distinct())
        {
            if (!string.IsNullOrEmpty(next)) dict[next] = GetTrigramCount(w1, w2, next);
        }
        return dict;
    }

    /// <summary>记录一次三元搭配。</summary>
    void AddTrigram(string w1, string w2, string next);

    /// <summary>按时间对历史频次做指数衰减（定期调用）。</summary>
    void DecayFrequencies(double factor);

    /// <summary>清空全部学习数据（隐私设置）。</summary>
    void Reset();
}
