using AiIme.Core.Models;

namespace AiIme.Core.Prediction;

/// <summary>
/// 本地语言模型预测抽象。第一版只预留接口与空实现，不绑定具体运行时
/// （后续可接 ONNX Runtime / llama.cpp 等本地推理）。
///
/// 设计原则：
/// - 纯本地推理，不发起网络请求；
/// - 模型不可用或未加载时，实现应返回空建议 / 中性分，调用方降级到统计排序；
/// - 与统计型 n-gram 学习解耦，可独立替换。
/// </summary>
public interface ILanguagePredictor
{
    /// <summary>模型是否已加载并可推理。false 时排序层应跳过模型分。</summary>
    bool IsAvailable { get; }

    /// <summary>
    /// 结合上下文，对给定候选给出模型置信度（0~1）。
    /// 无法判断时返回 0，不抛异常。
    /// </summary>
    double ScoreCandidate(string candidate, InputContext context);

    /// <summary>
    /// 基于上下文给出额外的续写建议（可能不在当前拼音召回集内）。
    /// 无建议时返回空集合。
    /// </summary>
    IReadOnlyList<PredictSuggestion> Suggest(InputContext context, int maxCount = 5);
}
