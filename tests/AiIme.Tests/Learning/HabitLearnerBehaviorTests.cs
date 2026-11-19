using AiIme.Core.Learning;
using AiIme.Core.Models;

namespace AiIme.Tests.Learning;

/// <summary>
/// 学习器行为测试：trigram 记录、多词上下文、衰减传播。
/// </summary>
public class HabitLearnerBehaviorTests
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
    public void RecordCommit_WritesTrigram_WhenTwoPreviousWords()
    {
        var store = new MemoryStore();
        var learner = new HabitLearner(store);
        var context = new InputContext { PreviousWords = new[] { "今天", "天气" } };

        learner.RecordCommit("不错", context);

        Assert.Equal(1, store.GetBigramCount("天气", "不错"));
        Assert.Equal(1, store.GetTrigramCount("今天", "天气", "不错"));
    }

    [Fact]
    public void RecordCommit_OnlyOnePreviousWord_WritesNoTrigram()
    {
        var store = new MemoryStore();
        var learner = new HabitLearner(store);
        var context = new InputContext { PreviousWords = new[] { "你好" } };

        learner.RecordCommit("世界", context);

        Assert.Equal(1, store.GetBigramCount("你好", "世界"));
        Assert.Equal(0, store.GetTrigramCount("你好", "世界", "世界"));
    }

    [Fact]
    public void RecordCommit_EmptyContext_WritesWordFreqOnly()
    {
        var store = new MemoryStore();
        var learner = new HabitLearner(store);

        learner.RecordCommit("新词", new InputContext());

        Assert.Equal(1.0, store.GetWordFrequency("新词"));
        Assert.Equal(0, store.GetBigramCount("新词", "新词"));
    }

    [Fact]
    public void Decay_ScalesStoredWordFrequencies_LeavesNgramCounts()
    {
        var store = new MemoryStore();
        var learner = new HabitLearner(store);
        store.AddWordFrequency("旧词", 10.0);
        store.AddBigram("前", "后");

        learner.Decay(0.5);

        Assert.Equal(5.0, store.GetWordFrequency("旧词"), 6);
        Assert.Equal(1, store.GetBigramCount("前", "后"));
    }

    [Fact]
    public void RecordCommit_NullOrEmptyWord_DoesNotWrite()
    {
        var store = new MemoryStore();
        var learner = new HabitLearner(store);

        learner.RecordCommit("", new InputContext());
        learner.RecordCommit("   ", new InputContext());

        Assert.Equal(0, store.GetWordFrequency(""));
        Assert.Empty(store.WordFreq);
    }
}
