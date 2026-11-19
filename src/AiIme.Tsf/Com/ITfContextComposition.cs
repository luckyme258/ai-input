using System.Runtime.InteropServices;

namespace AiIme.Tsf.Com;

/// <summary>
/// 上下文 composition 服务（由系统实现，从 ITfContext 经 QueryInterface 获取）。
/// 用于在编辑会话内创建/跟踪当前未上屏的输入区间。
/// vtable 顺序取自 windows-rs msctf 元数据；最小链路只用到 <see cref="StartComposition"/>，
/// 其余槽位按序占位，需要时再按序补齐。
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
    int StartComposition(uint ecWrite, IntPtr pCompositionRange, IntPtr pSink, out IntPtr ppComposition);

    // 占位槽位（按 vtable 顺序）：EnumCompositions / FindComposition / TakeOwnership。
}
