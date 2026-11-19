using AiIme.Core.Models;

namespace AiIme.Shell.Ui;

/// <summary>
/// 候选窗交互接口。第一版可用 WinForms/WPF 顶层窗口实现；
/// 接口把 UI 与核心逻辑解耦，便于后续换皮肤或改交互。
/// </summary>
public interface ICandidateView
{
    /// <summary>展示候选列表。</summary>
    void ShowCandidates(IReadOnlyList<Candidate> candidates);

    /// <summary>用户选中某候选时引发，参数为选中项。</summary>
    event EventHandler<Candidate>? CandidateSelected;

    /// <summary>用户取消组字（Esc）时引发。</summary>
    event EventHandler? Cancelled;
}
