using System.Runtime.InteropServices;

namespace AiIme.Tsf.Com;

/// <summary>
/// 原生 RECT 布局（msctf.h / winuser.h）：左上右下，屏幕或客户区坐标由调用方约定。
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct TfRect
{
    public int Left;
    public int Top;
    public int Right;
    public int Bottom;

    public int Width => Right - Left;
    public int Height => Bottom - Top;

    public bool IsEmpty => Right <= Left || Bottom <= Top;
}
