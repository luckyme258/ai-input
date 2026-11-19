namespace AiIme.Core.Models;

/// <summary>
/// 一条候选词。
/// </summary>
public sealed class Candidate
{
    /// <summary>显示给用户的文本（上屏文本）。</summary>
    public string Text { get; init; } = string.Empty;

    /// <summary>命中的拼音串（全拼或首字母）。</summary>
    public string Pinyin { get; init; } = string.Empty;

    /// <summary>来源：基础词库 / 用户学习 / 自造短语。</summary>
    public CandidateSource Source { get; init; }

    /// <summary>排序分（由 Ranking 层计算，越大越靠前）。</summary>
    public double Score { get; set; }

    /// <summary>基础通用词频（0~1，来自词库）。</summary>
    public double BaseFrequency { get; init; }
}

public enum CandidateSource
{
    /// <summary>内置/开源基础词库。</summary>
    Dictionary,

    /// <summary>用户历史输入学习得到。</summary>
    Learned,

    /// <summary>用户手动添加的自造词/短语。</summary>
    UserPhrase,
}
