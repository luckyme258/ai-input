using AiIme.Core.Dictionary;

namespace AiIme.Tests.Dictionary;

public class TrieDictionaryTests : IDisposable
{
    private readonly string _file;

    public TrieDictionaryTests()
    {
        _file = Path.Combine(Path.GetTempPath(), $"aiime-dict-{Guid.NewGuid():N}.tsv");
        File.WriteAllLines(_file, new[]
        {
            "你好\tni hao\tnh\t800",
            "你好吗\tni hao ma\tnhm\t200",
            "世界\tshi jie\tsj\t600",
            "时间\tshi jian\tsj\t500",
            "阿姨\ta yi\tay\t300",
        });
    }

    [Fact]
    public void Recall_FullPinyinPrefix_MatchesWords()
    {
        var dict = new TrieDictionary(_file);

        var results = dict.Recall("ni hao");

        Assert.Contains(results, c => c.Text == "你好");
        Assert.Contains(results, c => c.Text == "你好吗");
    }

    [Fact]
    public void Recall_InitialsPrefix_MatchesWords()
    {
        var dict = new TrieDictionary(_file);

        var results = dict.Recall("sj");

        Assert.Contains(results, c => c.Text == "世界");
        Assert.Contains(results, c => c.Text == "时间");
    }

    [Fact]
    public void Recall_PrefixOnly_MatchesDeeperWords()
    {
        var dict = new TrieDictionary(_file);

        var results = dict.Recall("shi");

        Assert.Contains(results, c => c.Text == "世界");
        Assert.Contains(results, c => c.Text == "时间");
    }

    [Fact]
    public void Recall_UnknownPrefix_ReturnsEmpty()
    {
        var dict = new TrieDictionary(_file);

        Assert.Empty(dict.Recall("zzz"));
    }

    [Fact]
    public void Recall_ContinuousFullPinyin_SegmentsAndMatches()
    {
        var dict = new TrieDictionary(_file);

        var results = dict.Recall("nihao");

        Assert.Contains(results, c => c.Text == "你好");
    }

    [Fact]
    public void Recall_PartialSecondSyllable_StillMatchesDeeperWord()
    {
        var dict = new TrieDictionary(_file);

        var results = dict.Recall("nih");

        Assert.Contains(results, c => c.Text == "你好吗");
    }

    [Fact]
    public void Recall_ResultsAreDeduplicated_AndOrderedByFrequency()
    {
        var dict = new TrieDictionary(_file);

        var results = dict.Recall("shi");

        Assert.Equal(results.Count, results.Select(c => c.Text).Distinct().Count());
        Assert.True(results[0].BaseFrequency >= results[^1].BaseFrequency);
    }

    [Fact]
    public void Load_Directory_MergesMultipleTsvFiles()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"aiime-dictdir-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        File.WriteAllLines(Path.Combine(dir, "a.tsv"), new[]
        {
            "你好\tni hao\tnh\t800",
        });
        File.WriteAllLines(Path.Combine(dir, "b.tsv"), new[]
        {
            "谢谢\txie xie\txx\t100",
        });

        var dict = new TrieDictionary(dir);
        dict.Load();

        Assert.Contains(dict.Recall("ni hao"), c => c.Text == "你好");
        Assert.Contains(dict.Recall("xie xie"), c => c.Text == "谢谢");

        Directory.Delete(dir, recursive: true);
    }

    public void Dispose()
    {
        if (File.Exists(_file)) File.Delete(_file);
    }
}
