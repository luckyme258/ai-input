using AiIme.Core.Learning;
using AiIme.Core.Models;
using AiIme.Core.Ranking;

namespace AiIme.Tests.Learning;

/// <summary>
/// 基于内存存储的最小测试，验证学习与排序的基本闭环。
/// </summary>
public class HabitLearnerTests
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

    [Fact]
    public void RecordCommit_IncreasesWordFrequency_AndWritesBigram()
    {
        var store = new MemoryStore();
        var learner = new HabitLearner(store);
        var context = new InputContext { PreviousWords = new[] { "你好" } };

        learner.RecordCommit("世界", context);

        Assert.Equal(1.0, store.GetWordFrequency("世界"));
        Assert.Equal(1, store.GetBigramCount("你好", "世界"));
    }

    [Fact]
    public void CorrectionCommit_AppliesExtraWeight()
    {
        var store = new MemoryStore();
        var learner = new HabitLearner(store) { CorrectionWeight = 2.0 };

        learner.RecordCommit("候选", new InputContext(), wasCorrection: true);

        Assert.Equal(2.0, store.GetWordFrequency("候选"));
    }

    [Fact]
    public void Rank_PrefersHigherUserFrequency()
    {
        var store = new MemoryStore();
        store.AddWordFrequency("常用词", 5.0);
        var ranker = new CandidateRanker(store);

        var candidates = new[]
        {
            new Candidate { Text = "冷门词", BaseFrequency = 0.1 },
            new Candidate { Text = "常用词", BaseFrequency = 0.1 },
        };

        var ranked = ranker.Rank(candidates, new InputContext());

        Assert.Equal("常用词", ranked[0].Text);
    }
}
