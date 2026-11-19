using AiIme.Core.Learning;
using Microsoft.Data.Sqlite;

namespace AiIme.Core.Store;

/// <summary>
/// SQLite 实现的用户记忆存储。
///
/// 表结构：
///   word_freq(word TEXT PK, freq REAL, updated_at TEXT)
///   bigram(prev TEXT, next TEXT, count INTEGER, PK(prev,next))
///   trigram(w1 TEXT, w2 TEXT, next TEXT, count INTEGER, PK(w1,w2,next))
///
/// 连接默认指向 %LOCALAPPDATA%\AiIme\profile.db；测试可传入自定义数据源路径。
/// </summary>
public sealed class SqliteProfileStore : IUserProfileStore, IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly bool _ownsConnection;

    /// <summary>使用默认本地数据库路径打开存储。</summary>
    public SqliteProfileStore()
        : this(NewConnectionString(DefaultDataSource()), ownsConnection: true)
    {
    }

    /// <summary>使用指定数据源（文件路径或 ":memory:"）打开存储，便于测试。</summary>
    public SqliteProfileStore(string dataSource)
        : this(NewConnectionString(dataSource), ownsConnection: true)
    {
    }

    private SqliteProfileStore(string connectionString, bool ownsConnection)
    {
        _connection = new SqliteConnection(connectionString);
        _ownsConnection = ownsConnection;
        _connection.Open();
        EnsureSchema();
    }

    // 注意：公共构造函数接收的是数据源路径；私有构造函数才接收连接串，
    // 不要把路径直接传给私有构造函数，否则连接串解析会报格式错误。

    private static string DefaultDataSource()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AiIme");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "profile.db");
    }

    private static string NewConnectionString(string dataSource) =>
        new SqliteConnectionStringBuilder { DataSource = dataSource, Mode = SqliteOpenMode.ReadWriteCreate }.ToString();

    private void EnsureSchema()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS word_freq (
                word       TEXT PRIMARY KEY,
                freq       REAL NOT NULL DEFAULT 0,
                updated_at TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS bigram (
                prev  TEXT NOT NULL,
                next  TEXT NOT NULL,
                count INTEGER NOT NULL DEFAULT 0,
                PRIMARY KEY (prev, next)
            );
            CREATE TABLE IF NOT EXISTS trigram (
                w1    TEXT NOT NULL,
                w2    TEXT NOT NULL,
                next  TEXT NOT NULL,
                count INTEGER NOT NULL DEFAULT 0,
                PRIMARY KEY (w1, w2, next)
            );
            """;
        cmd.ExecuteNonQuery();
    }

    public double GetWordFrequency(string word)
    {
        if (string.IsNullOrEmpty(word)) return 0;
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT freq FROM word_freq WHERE word = $word;";
        cmd.Parameters.AddWithValue("$word", word);
        var result = cmd.ExecuteScalar();
        return result is double d ? d : 0;
    }

    public void AddWordFrequency(string word, double weight = 1.0)
    {
        if (string.IsNullOrEmpty(word) || weight <= 0) return;
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO word_freq(word, freq, updated_at)
            VALUES ($word, $weight, $now)
            ON CONFLICT(word) DO UPDATE SET
                freq = freq + $weight,
                updated_at = $now;
            """;
        cmd.Parameters.AddWithValue("$word", word);
        cmd.Parameters.AddWithValue("$weight", weight);
        cmd.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O"));
        cmd.ExecuteNonQuery();
    }

    public long GetBigramCount(string previous, string next)
    {
        if (string.IsNullOrEmpty(previous) || string.IsNullOrEmpty(next)) return 0;
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT count FROM bigram WHERE prev = $prev AND next = $next;";
        cmd.Parameters.AddWithValue("$prev", previous);
        cmd.Parameters.AddWithValue("$next", next);
        var result = cmd.ExecuteScalar();
        return result is long l ? l : 0;
    }

    public void AddBigram(string previous, string next)
    {
        if (string.IsNullOrEmpty(previous) || string.IsNullOrEmpty(next)) return;
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO bigram(prev, next, count)
            VALUES ($prev, $next, 1)
            ON CONFLICT(prev, next) DO UPDATE SET count = count + 1;
            """;
        cmd.Parameters.AddWithValue("$prev", previous);
        cmd.Parameters.AddWithValue("$next", next);
        cmd.ExecuteNonQuery();
    }

    public long GetTrigramCount(string w1, string w2, string next)
    {
        if (string.IsNullOrEmpty(w1) || string.IsNullOrEmpty(w2) || string.IsNullOrEmpty(next)) return 0;
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT count FROM trigram WHERE w1 = $w1 AND w2 = $w2 AND next = $next;";
        cmd.Parameters.AddWithValue("$w1", w1);
        cmd.Parameters.AddWithValue("$w2", w2);
        cmd.Parameters.AddWithValue("$next", next);
        var result = cmd.ExecuteScalar();
        return result is long l ? l : 0;
    }

    public void AddTrigram(string w1, string w2, string next)
    {
        if (string.IsNullOrEmpty(w1) || string.IsNullOrEmpty(w2) || string.IsNullOrEmpty(next)) return;
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO trigram(w1, w2, next, count)
            VALUES ($w1, $w2, $next, 1)
            ON CONFLICT(w1, w2, next) DO UPDATE SET count = count + 1;
            """;
        cmd.Parameters.AddWithValue("$w1", w1);
        cmd.Parameters.AddWithValue("$w2", w2);
        cmd.Parameters.AddWithValue("$next", next);
        cmd.ExecuteNonQuery();
    }

    public void DecayFrequencies(double factor)
    {
        if (factor <= 0 || factor > 1) return;
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "UPDATE word_freq SET freq = freq * $factor;";
        cmd.Parameters.AddWithValue("$factor", factor);
        cmd.ExecuteNonQuery();
    }

    public void Reset()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            DELETE FROM word_freq;
            DELETE FROM bigram;
            DELETE FROM trigram;
            """;
        cmd.ExecuteNonQuery();
    }

    public void Dispose()
    {
        if (_ownsConnection)
        {
            _connection.Dispose();
        }
    }
}
