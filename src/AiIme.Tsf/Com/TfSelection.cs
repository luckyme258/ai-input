using System.Runtime.InteropServices;

namespace AiIme.Tsf.Com;

/// <summary>
/// 原生 TF_SELECTION 布局（msctf.h）：range 指针 + TF_SELECTIONSTYLE。
/// 与 windows-rs 元数据 <c>TF_SELECTION { range: *ITfRange; style: TF_SELECTIONSTYLE }</c> 一致。
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct TfSelection
{
    /// <summary>选区范围（ITfRange*）；GetSelection 由系统填充。</summary>
    public IntPtr Range;

    /// <summary>活动端：TF_AE_START=1 / TF_AE_END=2 / TF_AE_NONE=0。</summary>
    public int ActiveSelEnd;

    /// <summary>是否为临时字符（BOOL）。</summary>
    public int InterimChar;
}
