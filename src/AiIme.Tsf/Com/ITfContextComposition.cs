using System.Runtime.InteropServices;

namespace AiIme.Tsf.Com;

/// <summary>
/// 上下文 composition 服务（由系统实现，从 ITfContext 经 QueryInterface 获取）。
/// 用于在编辑会话内创建/跟踪当前未上屏的输入区间。
/// vtable 顺序（windows-rs ITfContextComposition_Vtbl 字段顺序，自有方法从槽位 3 起）：
/// StartComposition → EnumCompositions → FindComposition → TakeOwnership。
/// 后续占位槽位不得前移或后移，StartComposition 必须保持第一个自有方法。
/// </summary>
[ComImport]
[Guid("d40c8aae-ac92-4fc7-9a11-0ee0e23aa39b")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface ITfContextComposition
{
    /// <summary>
    /// 在指定范围上创建 composition 区间。
    /// </summary>
    /// <param name="ecWrite">写会话 cookie。</param>
    /// <param name="pCompositionRange">composition 覆盖范围（ITfRange*）。</param>
    /// <param name="pSink">可选 composition 终止回调（ITfCompositionSink*），可为 Zero。</param>
    /// <param name="ppComposition">输出创建出的 ITfComposition*。</param>
    [PreserveSig]
    int StartComposition(uint ecWrite, IntPtr pCompositionRange, IntPtr pSink, out IntPtr ppComposition);

    // 槽 4：EnumCompositions(IEnumITfCompositionView** ppEnum) —— 占位，当前不调用。
    int Placeholder_EnumCompositions(out IntPtr ppEnum);

    // 槽 5：FindComposition(ecRead, pTestRange, ppEnum) —— 占位，当前不调用。
    int Placeholder_FindComposition(uint ecRead, IntPtr pTestRange, out IntPtr ppEnum);

    // 槽 6：TakeOwnership(ecWrite, pCompositionView, pSink, ppComposition) —— 占位，当前不调用。
    int Placeholder_TakeOwnership(uint ecWrite, IntPtr pCompositionView, IntPtr pSink, out IntPtr ppComposition);
}
