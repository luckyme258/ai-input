using System.Runtime.InteropServices;

namespace AiIme.Tsf.Com;

/// <summary>
/// 上下文视图（系统实现）。从 <see cref="ITfContext.GetActiveView"/> 获取。
/// vtable 顺序取自 windows-rs msctf 元数据；当前自绘候选窗只需要 <see cref="GetWnd"/>，
/// 其余槽位按序占位，需要时再按序补齐，不得打乱顺序。
/// </summary>
[ComImport]
[Guid("2433bf8e-0f9b-435c-ba2c-180611978c30")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface ITfContextView
{
    // IUnknown 三槽由运行时保留；自有方法从槽位 3 起按元数据顺序排列。

    // 槽 3：GetRangeFromPoint（占位，当前不调用）。
    int GetRangeFromPoint(uint ec, IntPtr ppt, uint dwflags, out IntPtr pprange);

    // 槽 4：GetTextExt（返回选区在屏幕坐标下的外接矩形）。
    int GetTextExt(uint ec, IntPtr prange, ref TfRect prc, out int pfclipped);

    // 槽 5：GetScreenExt（占位，当前不调用）。
    int GetScreenExt(IntPtr prc);

    /// <summary>取该视图对应的宿主窗口句柄（HWND）。</summary>
    int GetWnd(out IntPtr phwnd);
}
