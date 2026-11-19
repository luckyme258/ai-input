using AiIme.Core.Models;

namespace AiIme.Core.Learning;

/// <summary>
/// 统计型习惯学习：在上屏后回写词频与 n-gram。
/// 不依赖任何输入法接口，可独立单测。
///
/// 学习时机：
/// - 正常上屏：词频 +1；
/// - 用户在候选中手动选择非默认首项：纠正加权，权重更高；
/// - 结合上一个/上两个已上屏词写入 bigram/trigram。
/// </summary>
public sealed class HabitLearner
{
    private readonly IUserProfileStore _store;

    /// <summary>手动纠正（选了非首项）时的额外权重。</summary>
    public double CorrectionWeight { get; init; } = 2.0;

    public HabitLearner(IUserProfileStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    /// <summary>
    /// 记录一次上屏。
    /// </summary>
    /// <param name="typed">本次上屏的词。</param>
    /// <param name="context">上屏前的上下文（含此前已上屏词）。</param>
    /// <param name="wasCorrection">是否为手动纠正（选择了非默认首项候选）。</param>
    public void RecordCommit(string typed, InputContext context, bool wasCorrection = false)
    {
        if (string.IsNullOrWhiteSpace(typed)) return;

        var weight = wasCorrection ? CorrectionWeight : 1.0;
        _store.AddWordFrequency(typed, weight);

        var prev = context.PreviousWords;
        if (prev.Count >= 1)
        {
            _store.AddBigram(prev[^1], typed);
        }

        if (prev.Count >= 2)
        {
            _store.AddTrigram(prev[^2], prev[^1], typed);
        }
    }

    /// <summary>定期衰减历史频次，避免旧习惯长期压制新词。</summary>
    public void Decay(double factor = 0.95) => _store.DecayFrequencies(factor);
}
