using AiIme.Core.Dictionary;
using AiIme.Core.Models;
using AiIme.Core.Ranking;

namespace AiIme.Core.Composition;

/// <summary>
/// 一次正在进行的拼音输入（composition）状态机。纯托管、不依赖 TSF/Win32，可单测。
///
/// 职责：累积拼音字母、处理退格/清空，调用词典召回并经排序器排序，
/// 产出当前候选列表。上屏（写回编辑框）由外壳层在拿到候选后完成。
/// </summary>
public sealed class CompositionState
{
    private readonly IPinyinDictionary _dictionary;
    private readonly CandidateRanker _ranker;

    /// <summary>当前已累积的拼音串（小写字母）。</summary>
    public string Pinyin { get; private set; } = string.Empty;

    /// <summary>当前候选（已排序）。</summary>
    public IReadOnlyList<Candidate> Candidates { get; private set; } = Array.Empty<Candidate>();

    /// <summary>是否处于有输入的 composition 状态。</summary>
    public bool IsComposing => Pinyin.Length > 0;

    public CompositionState(IPinyinDictionary dictionary, CandidateRanker ranker)
    {
        _dictionary = dictionary ?? throw new ArgumentNullException(nameof(dictionary));
        _ranker = ranker ?? throw new ArgumentNullException(nameof(ranker));
    }

    /// <summary>
    /// 追加一个按键字符。仅接受 a-z；忽略其它字符（数字/符号/空格等由外壳另行处理）。
    /// 返回该字符是否被本 composition 消费。
    /// </summary>
    public bool Append(char ch, InputContext? context = null)
    {
        if (!char.IsAsciiLetter(ch)) return false;

        Pinyin += char.ToLowerInvariant(ch);
        Refresh(context);
        return true;
    }

    /// <summary>退格删除最后一个拼音字母；返回是否删除了字符。</summary>
    public bool Backspace(InputContext? context = null)
    {
        if (Pinyin.Length == 0) return false;
        Pinyin = Pinyin[..^1];
        Refresh(context);
        return true;
    }

    /// <summary>清空 composition 与候选。</summary>
    public void Clear()
    {
        Pinyin = string.Empty;
        Candidates = Array.Empty<Candidate>();
    }

    private void Refresh(InputContext? context)
    {
        if (Pinyin.Length == 0)
        {
            Candidates = Array.Empty<Candidate>();
            return;
        }

        var recalled = _dictionary.Recall(Pinyin);
        Candidates = _ranker.Rank(recalled, context ?? new InputContext());
    }
}
