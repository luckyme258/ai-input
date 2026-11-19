using AiIme.Core.Learning;
using AiIme.Core.Models;
using AiIme.Core.Ranking;

namespace AiIme.Tests.Ranking;

/// <summary>
/// 候选排序打分测试：基础词频、个人词频、bigram/trigram 上下文搭配、会话内上浮。
/// 使用内存存储，不依赖 SQLite。
/// </summary>
public class CandidateRankerTests
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

    private static Candidate C(string text, double baseFreq = 0.0) =>
        new() { Text = text, BaseFrequency = baseFreq };

    [Fact]
    public void Rank_HigherBaseFrequency_RanksFirst_WhenNoUserHistory()
    {
        var store = new MemoryStore();
        var ranker = new CandidateRanker(store);

        var ranked = ranker.Rank(new[] { C("低频", 0.1), C("高频", 0.9) }, new InputContext());

        Assert.Equal("高频", ranked[0].Text);
        Assert.True(ranked[0].Score >= ranked[^1].Score);
    }

    [Fact]
    public void Rank_BigramContext_BoostsExpectedNextWord()
    {
        var store = new MemoryStore();
        // 上文“我”后面更常接“们”。
        store.AddBigram("我", "们");
        store.AddBigram("我", "们");
        store.AddBigram("我", "爱");
        var ranker = new CandidateRanker(store);

        var context = new InputContext { PreviousWords = new[] { "我" } };
        var ranked = ranker.Rank(new[] { C("爱"), C("们") }, context);

        Assert.Equal("们", ranked[0].Text);
    }

    [Fact]
    public void Rank_TrigramContext_ContributesOnTopOfBigram()
    {
        var store = new MemoryStore();
        // 三元搭配：“今天 天气 不错”。
        store.AddTrigram("今天", "天气", "不错");
        var ranker = new CandidateRanker(store);

        var context = new InputContext { PreviousWords = new[] { "今天", "天气" } };
        var ranked = ranker.Rank(new[] { C("很好"), C("不错") }, context);

        Assert.Equal("不错", ranked[0].Text);
    }

    [Fact]
    public void Rank_SessionRecentWord_GetRecentSessionBonus()
    {
        var store = new MemoryStore();
        var ranker = new CandidateRanker(store);

        var context = new InputContext { RecentSessionWords = new[] { "常用" } };
        var ranked = ranker.Rank(new[] { C("常用", 0.1), C("新词", 0.1) }, context);

        Assert.Equal("常用", ranked[0].Text);
    }

    [Fact]
    public void Rank_UserFrequency_OutweighsBaseFrequency()
    {
        var store = new MemoryStore();
        store.AddWordFrequency("个人偏好", 10.0);
        var ranker = new CandidateRanker(store);

        // 基础词频更高，但个人历史频次把“个人偏好”顶到前面。
        var ranked = ranker.Rank(new[] { C("通用", 0.9), C("个人偏好", 0.1) }, new InputContext());

        Assert.Equal("个人偏好", ranked[0].Text);
    }

    [Fact]
    public void Rank_EmptyContext_DoesNotThrow_AndReturnsOrdered()
    {
        var store = new MemoryStore();
        var ranker = new CandidateRanker(store);

        var ranked = ranker.Rank(new[] { C("甲", 0.3), C("乙", 0.5) }, new InputContext());

        Assert.Equal(2, ranked.Count);
        Assert.Equal("乙", ranked[0].Text);
    }

    [Fact]
    public void Rank_EmptyCandidateList_ReturnsEmpty()
    {
        var store = new MemoryStore();
        var ranker = new CandidateRanker(store);

        var ranked = ranker.Rank(Array.Empty<Candidate>(), new InputContext());

        Assert.Empty(ranked);
    }

    [Fact]
    public void Rank_ContextTail_IsLimitedToTwoWords()
    {
        var store = new MemoryStore();
        // 只记录最近二元搭配：“天气 不错”；更早的“今天”不应参与 trigram。
        store.AddBigram("天气", "不错");
        var ranker = new CandidateRanker(store);

        var context = new InputContext { PreviousWords = new[] { "今天", "天气" } };
        var ranked = ranker.Rank(new[] { C("不错"), C("很差") }, context);

        Assert.Equal("不错", ranked[0].Text);
        Assert.Equal(0, store.GetTrigramCount("今天", "天气", "不错"));
    }
}
