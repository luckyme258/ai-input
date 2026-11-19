using System.Runtime.InteropServices;

namespace AiIme.Tsf.Com;

/// <summary>
/// TSF 文档上下文（系统实现）。按键回调里的 pic 即此接口指针。
/// vtable 顺序取自 windows-rs msctf 元数据；最小上屏链路只实现
/// <see cref="RequestEditSession"/> 与 <see cref="GetSelection"/>，
/// 其余槽位按序占位，后续需要时在对应位置补齐，不得打乱顺序。
/// </summary>
[ComVisible(true)]
[ComImport]
[Guid("aa80e7fd-2021-11d2-93e0-0060b067b86e")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface ITfContext
{
    // IUnknown 三个槽位由 ComImport 运行时保留，下方自有方法从槽位 3 起按序声明。

    /// <summary>申请编辑会话；pes 为 ITfEditSession 实现，dwflags 见 TF_ES_*。</summary>
    int RequestEditSession(uint tid, IntPtr pes, uint dwflags, out int phrsession);

    /// <summary>查询当前是否处于写会话。</summary>
    int InWriteSession(uint tid, out int pfWriteSession);

    /// <summary>读取当前选区（返回首个选区范围）。</summary>
    int GetSelection(uint ec, uint ulIndex, uint ulCount, out TfSelection pSelection, out uint pcFetched);

    /// <summary>设置选区（占位，当前上屏链路不使用）。</summary>
    int SetSelection(uint ec, uint ulCount, IntPtr pSelection);

    /// <summary>取选区起点范围（占位槽位，当前不调用）。</summary>
    int GetStart(uint ec, out IntPtr ppRange);

    /// <summary>取选区终点范围（占位槽位，当前不调用）。</summary>
    int GetEnd(uint ec, out IntPtr ppRange);

    /// <summary>取当前活动视图（ITfContextView*，出参）。</summary>
    int GetActiveView(out IntPtr ppView);

    // 占位槽位（按 vtable 顺序）：EnumViews / GetStatus / GetProperty /
    // GetAppProperty / TrackProperties / EnumProperties / GetDocumentMgr / CreateRangeBackup。
    // 注意：ComImport 接口未声明的方法不会被运行时转发，需要时必须在此按序补入。
}
