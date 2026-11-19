using AiIme.Core.Composition;
using AiIme.Core.Dictionary;
using AiIme.Core.Learning;
using AiIme.Core.Models;
using AiIme.Core.Ranking;

namespace AiIme.Tests.Composition;

/// <summary>
/// CompositionState 按键状态机测试：字母累积、退格、清空、非字母忽略、召回排序接通。
/// 使用桩词典与桩排序器，不依赖真实词典与 SQLite。
/// </summary>
public class CompositionStateTests
{
    private sealed class StubDictionary : IPinyinDictionary
    {
        public string? LastQuery { get; private set; }
        public Dictionary<string, List<Candidate>> ByQuery { get; } = new();

        public IReadOnlyList<Candidate> Recall(string pinyinPrefix)
        {
            LastQuery = pinyinPrefix;
            return ByQuery.TryGetValue(pinyinPrefix, out var list) ? list : Array.Empty<Candidate>();
        }

        public void Load() { }
    }

    private sealed class StubRanker : CandidateRanker
    {
        public StubRanker() : base(new MemoryStore()) { }

        public override IReadOnlyList<Candidate> Rank(IEnumerable<Candidate> candidates, InputContext context) =>
            candidates.ToList();
    }

    private sealed class MemoryStore : IUserProfileStore
    {
        public double GetWordFrequency(string word) => 0;
        public void AddWordFrequency(string word, double weight = 1.0) { }
        public long GetBigramCount(string previous, string next) => 0;
        public void AddBigram(string previous, string next) { }
        public long GetTrigramCount(string w1, string w2, string next) => 0;
        public void AddTrigram(string w1, string w2, string next) { }
        public void DecayFrequencies(double factor) { }
        public void Reset() { }
    }

    private static CompositionState NewState(StubDictionary? dict = null, StubRanker? ranker = null)
    {
        dict ??= new StubDictionary();
        ranker ??= new StubRanker();
        return new CompositionState(dict, ranker);
    }

    [Fact]
    public void Append_LowercasesLetters_AndBuildsPinyin()
    {
        var state = NewState();

        Assert.True(state.Append('N'));
        Assert.True(state.Append('I'));

        Assert.Equal("ni", state.Pinyin);
        Assert.True(state.IsComposing);
    }

    [Fact]
    public void Append_NonLetter_IsIgnored_AndNotConsumed()
    {
        var state = NewState();

        Assert.False(state.Append('5'));
        Assert.False(state.Append(' '));
        Assert.False(state.Append('好'));

        Assert.Equal(string.Empty, state.Pinyin);
        Assert.False(state.IsComposing);
    }

    [Fact]
    public void Backspace_RemovesLastLetter()
    {
        var state = NewState();
        state.Append('n');
        state.Append('i');

        Assert.True(state.Backspace());
        Assert.Equal("n", state.Pinyin);

        Assert.True(state.Backspace());
        Assert.Equal(string.Empty, state.Pinyin);
        Assert.False(state.IsComposing);
    }

    [Fact]
    public void Backspace_WhenEmpty_DoesNothing()
    {
        var state = NewState();
        Assert.False(state.Backspace());
    }

    [Fact]
    public void Clear_ResetsPinyinAndCandidates()
    {
        var dict = new StubDictionary();
        dict.ByQuery["ni"] = new List<Candidate> { new() { Text = "你好" } };
        var state = NewState(dict, new StubRanker());

        state.Append('n');
        state.Append('i');
        Assert.NotEmpty(state.Candidates);

        state.Clear();
        Assert.Equal(string.Empty, state.Pinyin);
        Assert.Empty(state.Candidates);
    }

    [Fact]
    public void Refresh_CallsDictionaryWithCurrentPinyin_AndRanksCandidates()
    {
        var dict = new StubDictionary();
        dict.ByQuery["ni"] = new List<Candidate>
        {
            new() { Text = "你好" },
            new() { Text = "你们" },
        };
        var state = NewState(dict, new StubRanker());

        state.Append('n');
        state.Append('i');

        Assert.Equal("ni", dict.LastQuery);
        Assert.Equal(2, state.Candidates.Count);
    }

    [Fact]
    public void EmptyPinyin_ProducesNoCandidates()
    {
        var state = NewState();
        Assert.Empty(state.Candidates);
    }
}
