namespace AiIme.Core.Prediction;

/// <summary>
/// 本地语言模型给出的一条续写/候选建议。
/// 模型分约定为 0~1 的归一化概率或置信度，排序层再按权重融合。
/// </summary>
public sealed class PredictSuggestion
{
    /// <summary>建议上屏的词或短语。</summary>
    public string Text { get; init; } = string.Empty;

    /// <summary>模型置信度（0~1，越大越可信）。</summary>
    public double Score { get; init; }

    /// <summary>建议类型：单词预测 / 整句续写。</summary>
    public SuggestionKind Kind { get; init; } = SuggestionKind.Word;
}

/// <summary>模型建议类型。</summary>
public enum SuggestionKind
{
    /// <summary>预测下一个词。</summary>
    Word,

    /// <summary>基于上下文给出整句续写片段。</summary>
    Phrase,
}
