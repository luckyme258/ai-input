using AiIme.Core.Learning;
using AiIme.Core.Models;
using AiIme.Core.Prediction;

namespace AiIme.Core.Ranking;

/// <summary>
/// 候选打分排序。第一版为可解释的加权融合，权重做成配置项便于后续调参。
///
/// score = w1 * 基础通用词频
///       + w2 * 个人加权词频（衰减后）
///       + w3 * 上下文搭配分（bigram / trigram 命中率）
///       + w4 * 会话内近期上浮
///       + w5 * 本地模型置信度（未接入模型时为 0）
/// </summary>
public class CandidateRanker
{
    private readonly IUserProfileStore _store;
    private readonly ILanguagePredictor _predictor;

    public double WeightBaseFrequency { get; init; } = 1.0;
    public double WeightUserFrequency { get; init; } = 2.0;
    public double WeightContext { get; init; } = 1.5;
    public double WeightRecentSession { get; init; } = 0.8;

    /// <summary>本地模型置信度权重。模型不可用时该项恒为 0。</summary>
    public double WeightModelScore { get; init; } = 1.0;

    public CandidateRanker(IUserProfileStore store, ILanguagePredictor? predictor = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _predictor = predictor ?? new NullPredictor();
    }

    /// <summary>对候选集打分并返回按分数降序排列的结果。</summary>
    public virtual IReadOnlyList<Candidate> Rank(IEnumerable<Candidate> candidates, InputContext context)
    {
        var list = candidates.ToList();
        var tail2 = context.Tail(2);

        foreach (var c in list)
        {
            c.Score = Score(c, context, tail2);
        }

        return list.OrderByDescending(c => c.Score).ToList();
    }

    private double Score(Candidate c, InputContext context, IReadOnlyList<string> tail2)
    {
        double score = 0;

        score += WeightBaseFrequency * c.BaseFrequency;

        var userFreq = _store.GetWordFrequency(c.Text);
        score += WeightUserFrequency * userFreq;

        score += WeightContext * ContextScore(c.Text, tail2);

        if (context.RecentSessionWords.Contains(c.Text))
        {
            score += WeightRecentSession;
        }

        if (_predictor.IsAvailable)
        {
            score += WeightModelScore * _predictor.ScoreCandidate(c.Text, context);
        }

        return score;
    }

    private double ContextScore(string next, IReadOnlyList<string> tail2)
    {
        double score = 0;

        if (tail2.Count >= 1)
        {
            var bigram = _store.GetBigramCount(tail2[^1], next);
            score += Math.Log10(1 + bigram);
        }

        if (tail2.Count >= 2)
        {
            var trigram = _store.GetTrigramCount(tail2[^2], tail2[^1], next);
            score += Math.Log10(1 + trigram);
        }

        return score;
    }
}
