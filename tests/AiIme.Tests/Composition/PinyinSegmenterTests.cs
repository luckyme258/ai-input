using AiIme.Core.Composition;

namespace AiIme.Tests.Composition;

public class PinyinSegmenterTests
{
    private static readonly HashSet<string> Syllables = new(StringComparer.Ordinal)
    {
        "ni", "hao", "ma", "shi", "jie", "jian", "a", "yi", "niang", "huang",
    };

    [Fact]
    public void Segment_ContinuousPinyin_SplitsIntoSyllables()
    {
        var tokens = PinyinSegmenter.Segment("nihao", Syllables);

        Assert.Equal(new[] { "ni", "hao" }, tokens);
    }

    [Fact]
    public void Segment_PartialLastSyllable_KeepsFragment()
    {
        var tokens = PinyinSegmenter.Segment("nih", Syllables);

        Assert.Equal(new[] { "ni", "h" }, tokens);
    }

    [Fact]
    public void Segment_Spaces_AreSplitDirectly()
    {
        var tokens = PinyinSegmenter.Segment("ni hao", Syllables);

        Assert.Equal(new[] { "ni", "hao" }, tokens);
    }

    [Fact]
    public void Segment_UnknownLetters_BecomeSingleCharFragments()
    {
        var tokens = PinyinSegmenter.Segment("sj", Syllables);

        Assert.Equal(new[] { "s", "j" }, tokens);
    }

    [Fact]
    public void Segment_MaximumMatch_PrefersLongerSyllable()
    {
        // niang 优先于 ni + ang 残片。
        var tokens = PinyinSegmenter.Segment("niang", Syllables);

        Assert.Equal(new[] { "niang" }, tokens);
    }
}
