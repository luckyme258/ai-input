using AiIme.Core.Models;

namespace AiIme.Core.Prediction;

/// <summary>
/// 空预测器：未接入本地模型时的默认实现。不产生任何模型分或建议，
/// 排序层据此完全走统计型打分。
/// </summary>
public sealed class NullPredictor : ILanguagePredictor
{
    public bool IsAvailable => false;

    public double ScoreCandidate(string candidate, InputContext context) => 0.0;

    public IReadOnlyList<PredictSuggestion> Suggest(InputContext context, int maxCount = 5) =>
        Array.Empty<PredictSuggestion>();
}
