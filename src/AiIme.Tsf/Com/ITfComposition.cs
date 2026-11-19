using System.Runtime.InteropServices;

namespace AiIme.Tsf.Com;

/// <summary>
/// 进行中的 composition（拼音串区间）。由 ITfContextComposition::StartComposition 创建，
/// 用于跟踪当前未上屏的输入区间；结束时调 <see cref="EndComposition"/>。
/// vtable 顺序取自 windows-rs msctf 元数据（4 个槽位）。
/// </summary>
[ComImport]
[Guid("20168d64-5a8f-4a5a-b7bd-cfa29f4d0fd9")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface ITfComposition
{
    /// <summary>取 composition 覆盖的文本范围。</summary>
    int GetRange(out IntPtr ppRange);

    /// <summary>移动 composition 起点到指定范围。</summary>
    int ShiftStart(uint ec, IntPtr pNewStart);

    /// <summary>移动 composition 终点到指定范围。</summary>
    int ShiftEnd(uint ec, IntPtr pNewEnd);

    /// <summary>结束 composition。</summary>
    int EndComposition(uint ecWrite);
}
