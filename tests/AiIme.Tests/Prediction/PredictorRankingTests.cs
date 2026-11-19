using AiIme.Core.Learning;
using AiIme.Core.Models;
using AiIme.Core.Prediction;
using AiIme.Core.Ranking;

namespace AiIme.Tests.Prediction;

/// <summary>
/// 本地模型预测接口的预留测试：验证模型分作为一路加权融入排序，
/// 以及模型不可用时完全降级到统计打分。
/// </summary>
public class PredictorRankingTests
{
    private sealed class MemoryStore : IUserProfileStore
    {
        public Dictionary<string, double> WordFreq { get; } = new();
        public Dictionary<(string, string), long> Bigram { get; } = new();
        public Dictionary<(string, string, string), long> Trigram { get; } = new();

        public double GetWordFrequency(string word) => WordFreq.TryGetValue(word, out var f) ? f : 0;
        public void AddWordFrequency(string word, double weight = 1.0) => WordFreq[word] = GetWordFrequency(word) + weight;
        public long GetBigramCount(string previous, string next) => Bigram.TryGetValue((previous, next), out var c) ? c : 0;
        public void AddBigram(string previous, string next) => Bigram[(previous, next)] = GetBigramCount(previous, next) + 1;
        public long GetTrigramCount(string w1, string w2, string next) => Trigram.TryGetValue((w1, w2, next), out var c) ? c : 0;
        public void AddTrigram(string w1, string w2, string next) => Trigram[(w1, w2, next)] = GetTrigramCount(w1, w2, next) + 1;
        public void DecayFrequencies(double factor) { foreach (var k in WordFreq.Keys.ToList()) WordFreq[k] *= factor; }
        public void Reset() { WordFreq.Clear(); Bigram.Clear(); Trigram.Clear(); }
    }

    private sealed class FakePredictor : ILanguagePredictor
    {
        private readonly Dictionary<string, double> _scores;
        public bool IsAvailable { get; init; } = true;

        public FakePredictor(Dictionary<string, double> scores) => _scores = scores;

        public double ScoreCandidate(string candidate, InputContext context) =>
            _scores.TryGetValue(candidate, out var s) ? s : 0.0;

        public IReadOnlyList<PredictSuggestion> Suggest(InputContext context, int maxCount = 5) =>
            Array.Empty<PredictSuggestion>();
    }

    private static Candidate C(string text, double baseFreq = 0.0) =>
        new() { Text = text, BaseFrequency = baseFreq };

    [Fact]
    public void NullPredictor_IsNotAvailable_AndReturnsEmpty()
    {
        var predictor = new NullPredictor();

        Assert.False(predictor.IsAvailable);
        Assert.Equal(0.0, predictor.ScoreCandidate("任意", new InputContext()));
        Assert.Empty(predictor.Suggest(new InputContext()));
    }

    [Fact]
    public void Rank_ModelScore_BoostsCandidate_WhenPredictorAvailable()
    {
        var store = new MemoryStore();
        var predictor = new FakePredictor(new Dictionary<string, double> { ["模型偏好"] = 0.9 });
        var ranker = new CandidateRanker(store, predictor) { WeightModelScore = 1.0 };

        // 基础词频相同，模型分把“模型偏好”顶到前面。
        var ranked = ranker.Rank(new[] { C("模型偏好", 0.1), C("普通", 0.1) }, new InputContext());

        Assert.Equal("模型偏好", ranked[0].Text);
    }

    [Fact]
    public void Rank_UnavailablePredictor_IgnoresModelScore()
    {
        var store = new MemoryStore();
        var predictor = new FakePredictor(new Dictionary<string, double> { ["高分项"] = 0.9 })
        {
            IsAvailable = false,
        };
        var ranker = new CandidateRanker(store, predictor);

        var ranked = ranker.Rank(new[] { C("高分项", 0.1), C("普通", 0.5) }, new InputContext());

        // 模型分被跳过，按基础词频排序。
        Assert.Equal("普通", ranked[0].Text);
    }

    [Fact]
    public void Rank_DefaultConstructor_UsesNullPredictor()
    {
        var store = new MemoryStore();
        var ranker = new CandidateRanker(store);

        var ranked = ranker.Rank(new[] { C("甲", 0.2), C("乙", 0.4) }, new InputContext());

        Assert.Equal("乙", ranked[0].Text);
    }
}
