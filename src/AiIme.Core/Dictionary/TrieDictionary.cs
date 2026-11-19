using AiIme.Core.Composition;
using AiIme.Core.Models;

namespace AiIme.Core.Dictionary;

/// <summary>
/// 基于前缀树（Trie）的拼音词典。
/// 同时建立全拼索引与首字母索引，支持：
/// - 全拼前缀：如 "nihao" 切分为 ni/hao -> "你好(ni hao)"；
/// - 首字母前缀：如 "nh" -> "你好"。
/// 数据来自 TSV：word\tpinyin(空格分隔)\tinitials\tbaseFreq。
/// </summary>
public sealed class TrieDictionary : IPinyinDictionary
{
    private sealed class Node
    {
        public Dictionary<char, Node> Children { get; } = new();
        public List<Candidate> Words { get; } = new();
    }

    private readonly string _dataPath;
    private readonly Node _fullRoot = new();
    private readonly Node _initialRoot = new();
    private readonly HashSet<string> _syllables = new(StringComparer.Ordinal);
    private bool _loaded;

    /// <summary>一次召回返回的基础候选上限，避免单字母前缀带出整棵子树撑爆候选窗。</summary>
    public int MaxRecall { get; init; } = 300;

    /// <summary>基础词频归一化上限，避免高频词在打分时压过个人权重。</summary>
    public double MaxBaseFrequency { get; init; } = 10000.0;

    /// <summary>
    /// 构造词典。<paramref name="dataPath"/> 可以是单个 TSV 文件，
    /// 也可以是包含多个 TSV 的目录（目录下按文件名排序合并加载）。
    /// </summary>
    public TrieDictionary(string dataPath)
    {
        _dataPath = dataPath ?? throw new ArgumentNullException(nameof(dataPath));
    }

    public void Load()
    {
        _fullRoot.Children.Clear();
        _fullRoot.Words.Clear();
        _initialRoot.Children.Clear();
        _initialRoot.Words.Clear();
        _syllables.Clear();

        var files = ResolveFiles();
        foreach (var file in files)
        {
            LoadFile(file);
        }

        _loaded = true;
    }

    private IEnumerable<string> ResolveFiles()
    {
        if (Directory.Exists(_dataPath))
        {
            return Directory.EnumerateFiles(_dataPath, "*.tsv")
                .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        return File.Exists(_dataPath) ? new[] { _dataPath } : Array.Empty<string>();
    }

    private void LoadFile(string file)
    {
        foreach (var raw in File.ReadLines(file))
        {
            if (string.IsNullOrWhiteSpace(raw)) continue;
            var parts = raw.Split('\t');
            if (parts.Length < 3) continue;

            var word = parts[0].Trim();
            var pinyin = parts[1].Trim();
            var initials = parts[2].Trim();
            var freq = parts.Length >= 4 && double.TryParse(parts[3], out var f) ? f : 1.0;
            if (string.IsNullOrEmpty(word) || string.IsNullOrEmpty(pinyin)) continue;

            foreach (var syllable in pinyin.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                _syllables.Add(syllable);
            }

            var candidate = new Candidate
            {
                Text = word,
                Pinyin = pinyin,
                Source = CandidateSource.Dictionary,
                BaseFrequency = Normalize(freq),
            };

            Insert(_fullRoot, pinyin.Replace(' ', '\0'), candidate);
            Insert(_initialRoot, initials, candidate);
        }
    }

    private double Normalize(double freq) =>
        freq <= 0 ? 0 : Math.Min(1.0, freq / MaxBaseFrequency);

    private static void Insert(Node root, string key, Candidate candidate)
    {
        var node = root;
        foreach (var ch in key)
        {
            if (!node.Children.TryGetValue(ch, out var child))
            {
                child = new Node();
                node.Children[ch] = child;
            }
            node = child;
        }
        // 同一词条可能有多读音，去重保留首条。
        if (!node.Words.Any(w => w.Text == candidate.Text))
        {
            node.Words.Add(candidate);
        }
    }

    public IReadOnlyList<Candidate> Recall(string pinyinPrefix)
    {
        if (!_loaded) Load();
        if (string.IsNullOrWhiteSpace(pinyinPrefix)) return Array.Empty<Candidate>();

        var key = pinyinPrefix.Trim().ToLowerInvariant();
        var results = new List<Candidate>();

        // 1. 全拼：先切分为音节序列，按音节前缀在全拼索引上召回。
        var tokens = PinyinSegmenter.Segment(key, _syllables);
        CollectFull(_fullRoot, tokens, results);

        // 2. 首字母兜底：全拼无命中时，再按首字母索引补充（如 "sj" -> 世界/时间）。
        if (results.Count == 0)
        {
            Collect(_initialRoot, key, results);
        }

        return results
            .GroupBy(c => c.Text)
            .Select(g => g.First())
            .OrderByDescending(c => c.BaseFrequency)
            .Take(MaxRecall)
            .ToList();
    }

    /// <summary>
    /// 按音节序列在全拼索引上导航：除最后一个音节外，每个音节后必须跟音节分隔符 \0；
    /// 最后一个音节允许是未拼完的前缀，收集其整棵子树。
    /// </summary>
    private static void CollectFull(Node root, IReadOnlyList<string> tokens, List<Candidate> results)
    {
        if (tokens.Count == 0) return;

        var node = root;
        for (int t = 0; t < tokens.Count; t++)
        {
            var token = tokens[t];
            bool last = t == tokens.Count - 1;

            foreach (var ch in token)
            {
                if (!node.Children.TryGetValue(ch, out var child)) return;
                node = child;
            }

            if (!last)
            {
                if (!node.Children.TryGetValue('\0', out var separator)) return;
                node = separator;
            }
        }

        CollectSubtree(node, results);
    }

    private static void Collect(Node root, string key, List<Candidate> results)
    {
        var node = root;
        foreach (var ch in key)
        {
            if (!node.Children.TryGetValue(ch, out var child)) return;
            node = child;
        }

        CollectSubtree(node, results);
    }

    private static void CollectSubtree(Node node, List<Candidate> results)
    {
        var stack = new Stack<Node>();
        stack.Push(node);
        while (stack.Count > 0)
        {
            var current = stack.Pop();
            results.AddRange(current.Words);
            foreach (var child in current.Children.Values)
            {
                stack.Push(child);
            }
        }
    }
}
