namespace AiIme.Core.Learning;

/// <summary>
/// 用户习惯记忆的持久化接口。第一版用 SQLite 实现；
/// 该接口把存储与学习算法解耦，便于单测与后续替换。
/// </summary>
public interface IUserProfileStore
{
    /// <summary>读取某词的个人累计频次（未记录返回 0）。</summary>
    double GetWordFrequency(string word);

    /// <summary>累加一个词的频次；<paramref name="weight"/> 用于纠正加权等场景。</summary>
    void AddWordFrequency(string word, double weight = 1.0);

    /// <summary>读取二元搭配计数：上文词 -> 下文词。</summary>
    long GetBigramCount(string previous, string next);

    /// <summary>记录一次二元搭配。</summary>
    void AddBigram(string previous, string next);

    /// <summary>读取三元搭配计数：(w1,w2) -> next。</summary>
    long GetTrigramCount(string w1, string w2, string next);

    /// <summary>记录一次三元搭配。</summary>
    void AddTrigram(string w1, string w2, string next);

    /// <summary>按时间对历史频次做指数衰减（定期调用）。</summary>
    void DecayFrequencies(double factor);

    /// <summary>清空全部学习数据（隐私设置）。</summary>
    void Reset();
}
