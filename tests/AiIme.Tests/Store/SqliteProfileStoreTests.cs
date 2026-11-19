using AiIme.Core.Store;

namespace AiIme.Tests.Store;

/// <summary>
/// SQLite 存储集成测试：使用临时文件库，验证词频、bigram/trigram、衰减与清空。
/// </summary>
public class SqliteProfileStoreTests : IDisposable
{
    private readonly SqliteProfileStore _store;

    public SqliteProfileStoreTests()
    {
        // 使用内存库，避免文件句柄释放延迟导致的删除锁。
        _store = new SqliteProfileStore(":memory:");
    }

    [Fact]
    public void WordFrequency_Accumulates_AcrossAdds()
    {
        _store.AddWordFrequency("你好", 1.0);
        _store.AddWordFrequency("你好", 2.0);

        Assert.Equal(3.0, _store.GetWordFrequency("你好"), 6);
    }

    [Fact]
    public void Bigram_And_Trigram_Count_Increment()
    {
        _store.AddBigram("你好", "世界");
        _store.AddBigram("你好", "世界");
        _store.AddTrigram("今天", "天气", "不错");

        Assert.Equal(2, _store.GetBigramCount("你好", "世界"));
        Assert.Equal(1, _store.GetTrigramCount("今天", "天气", "不错"));
        Assert.Equal(0, _store.GetBigramCount("缺失", "词条"));
    }

    [Fact]
    public void DecayFrequencies_ScalesDownWordFreq_ButKeepsNgramCounts()
    {
        _store.AddWordFrequency("高频词", 10.0);
        _store.AddBigram("前", "后");

        _store.DecayFrequencies(0.5);

        Assert.Equal(5.0, _store.GetWordFrequency("高频词"), 6);
        // n-gram 计数不参与词频衰减。
        Assert.Equal(1, _store.GetBigramCount("前", "后"));
    }

    [Fact]
    public void Reset_ClearsAllTables()
    {
        _store.AddWordFrequency("词", 1.0);
        _store.AddBigram("a", "b");
        _store.AddTrigram("x", "y", "z");

        _store.Reset();

        Assert.Equal(0, _store.GetWordFrequency("词"));
        Assert.Equal(0, _store.GetBigramCount("a", "b"));
        Assert.Equal(0, _store.GetTrigramCount("x", "y", "z"));
    }

    [Fact]
    public void EmptyOrNull_Inputs_DoNotThrow_AndReturnZero()
    {
        Assert.Equal(0, _store.GetWordFrequency(""));
        _store.AddWordFrequency("", 1.0);
        Assert.Equal(0, _store.GetBigramCount("", "x"));
        _store.AddTrigram("", "y", "z");
    }

    public void Dispose()
    {
        _store.Dispose();
    }
}
