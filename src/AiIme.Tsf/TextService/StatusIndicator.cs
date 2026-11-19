using System.Runtime.InteropServices;
using System.Text;
using AiIme.Tsf.Com;

namespace AiIme.Tsf.TextService;

/// <summary>
/// 自绘模式状态条：原生 Win32 置顶小弹出窗，在光标附近显示当前输入模式
/// 「中」（拼音）/「EN」（英文直输）。不依赖 WPF，避免 comhost 拉取桌面框架。
///
/// 线程约定同 <see cref="CandidateWindow"/>：窗口创建与消息处理都在宿主进程线程内，
/// 不另起消息循环；只做静态展示，不处理交互消息。
/// </summary>
internal sealed class StatusIndicator : IDisposable
{
    private const int WS_POPUP = unchecked((int)0x80000000);
    private const int WS_EX_TOPMOST = 0x00000008;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_NOACTIVATE = 0x08000000;

    private const int WM_DESTROY = 0x0002;
    private const int WM_PAINT = 0x000F;
    private const int WM_ERASEBKGND = 0x0014;

    private const int DT_LEFT = 0x0000;
    private const int DT_SINGLELINE = 0x0020;
    private const int DT_CENTER = 0x0001;

    private const int Height = 26;
    private const int PaddingX = 10;
    private const int MinWidth = 46;
    private const int FontHeight = -15;

    // COLORREF 为 0x00BBGGRR。
    private const uint ZhBg = 0x00D77800;       // 蓝 RGB(0,120,215)
    private const uint ZhText = 0x00FFFFFF;     // 白
    private const uint EnBg = 0x00F2F2F2;       // 浅灰
    private const uint EnText = 0x00202020;     // 深灰

    private static readonly WndProcDelegate WndProc = OnWndProc;
    private static readonly StringBuilder ClassName = new("AiImeStatusWnd");

    private IntPtr _hwnd;
    private bool _classRegistered;
    private bool _visible;
    private bool _english;
    private string _label = string.Empty;
    private int _lastX = int.MinValue;
    private int _lastY = int.MinValue;
    private int _lastWidth;
    private int _lastHeight;
    private IntPtr _lastGoodHost;
    private TfRect _lastGoodCaret;
    private bool _hasLastGood;
    private bool _disposed;

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SIZE
    {
        public int cx;
        public int cy;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WNDCLASSEX
    {
        public int cbSize;
        public int style;
        public WndProcDelegate lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        public string lpszMenuName;
        public string lpszClassName;
        public IntPtr hIconSm;
    }

    private delegate IntPtr WndProcDelegate(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int RegisterClassEx(ref WNDCLASSEX lpwcx);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateWindowEx(int dwExStyle, string lpClassName, string lpWindowName,
        int dwStyle, int x, int y, int nWidth, int nHeight, IntPtr hWndParent, IntPtr hMenu,
        IntPtr hInstance, IntPtr lpParam);

    [DllImport("user32.dll")]
    private static extern bool DestroyWindow(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hwnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hwnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

    [DllImport("user32.dll")]
    private static extern bool GetClientRect(IntPtr hwnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern bool InvalidateRect(IntPtr hwnd, IntPtr lpRect, bool bErase);

    [DllImport("user32.dll")]
    private static extern bool ValidateRect(IntPtr hwnd, IntPtr lpRect);

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);

    [DllImport("user32.dll")]
    private static extern IntPtr CreateSolidBrush(uint crColor);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr ho);

    [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateFontW(int nHeight, int nWidth, int nEscapement, int nOrientation,
        int nWeight, uint dwItalic, uint dwUnderline, uint dwStrikeOut, uint dwCharSet,
        uint dwOutputPrecision, uint dwClipPrecision, uint dwQuality, uint dwPitchAndFamily, string lpszFace);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int cx, int cy);

    [DllImport("gdi32.dll")]
    private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hobj);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern int BitBlt(IntPtr hdcDst, int x, int y, int w, int h, IntPtr hdcSrc, int xSrc, int ySrc, uint rop);

    [DllImport("user32.dll")]
    private static extern bool FillRect(IntPtr hdc, ref RECT lprc, IntPtr hbr);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int DrawText(IntPtr hdc, string lpchText, int nCount, ref RECT lpRect, uint uFormat);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    [DllImport("user32.dll")]
    private static extern bool ClientToScreen(IntPtr hwndParent, ref POINT lpPoint);

    [DllImport("gdi32.dll")]
    private static extern int SetTextColor(IntPtr hdc, uint crColor);

    [DllImport("gdi32.dll")]
    private static extern int SetBkMode(IntPtr hdc, int mode);

    [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
    private static extern bool GetTextExtentPoint32W(IntPtr hdc, string lpString, int cbString, out SIZE size);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr DefWindowProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

    private const int FwNormal = 400;
    private const uint DefaultCharset = 134; // GB2312，保证中文回退
    private const string FaceName = "Microsoft YaHei";

    private static readonly IntPtr HWND_TOPMOST = new(-1);
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const int SW_SHOW = 5;
    private const int SW_HIDE = 0;
    private const uint SRCCOPY = 0x00CC0020;

    private static readonly Dictionary<IntPtr, StatusIndicator> _instances = new();

    /// <summary>按当前模式刷新并显示指示条。</summary>
    /// <param name="english">true=英文直输（EN），false=拼音（中）。</param>
    public void SetMode(bool english, IntPtr parentHwnd, TfRect caret)
    {
        if (_disposed) return;

        _english = english;
        _label = english ? "EN" : "中";

        EnsureWindow();
        if (_hwnd == IntPtr.Zero) return;

        PositionNear(parentHwnd == _hwnd ? IntPtr.Zero : parentHwnd, caret);
        if (!_visible)
        {
            ShowWindow(_hwnd, SW_SHOW);
            _visible = true;
        }

        InvalidateRect(_hwnd, IntPtr.Zero, false);
        Paint(_hwnd);
        TsfDebugLog.Write("StatusWnd", $"mode={(english ? "EN" : "ZH")} label='{_label}'");
    }

    /// <summary>隐藏指示条。</summary>
    public void Hide()
    {
        if (_hwnd != IntPtr.Zero && _visible)
        {
            ShowWindow(_hwnd, SW_HIDE);
            _visible = false;
            _lastX = int.MinValue;
            _lastY = int.MinValue;
        }
    }

    private void EnsureWindow()
    {
        if (_hwnd != IntPtr.Zero) return;

        if (!_classRegistered)
        {
            var wc = new WNDCLASSEX
            {
                cbSize = Marshal.SizeOf<WNDCLASSEX>(),
                lpfnWndProc = WndProc,
                hbrBackground = IntPtr.Zero,
                lpszClassName = ClassName.ToString(),
            };
            if (RegisterClassEx(ref wc) != 0) _classRegistered = true;
        }

        _hwnd = CreateWindowEx(
            WS_EX_TOPMOST | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE,
            ClassName.ToString(), string.Empty, WS_POPUP,
            0, 0, MinWidth, Height,
            IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);

        if (_hwnd != IntPtr.Zero) _instances[_hwnd] = this;
    }

    private void PositionNear(IntPtr parentHwnd, TfRect caret)
    {
        int width = MeasureWidth();
        int height = Height;

        int screenW = GetSystemMetrics(0);
        int screenH = GetSystemMetrics(1);

        IntPtr host = parentHwnd;
        int x, y;

        bool corner = caret.Left >= screenW - 4 || caret.Top >= screenH - 4
            || (caret.Left == 0 && caret.Top == 0 && caret.Right == 0 && caret.Bottom == 0);
        bool hasCaret = !corner && (caret.Width > 0 || caret.Height > 0);

        if (hasCaret)
        {
            _lastGoodCaret = caret;
            _hasLastGood = true;
            if (host != IntPtr.Zero) _lastGoodHost = host;
        }
        else if (_hasLastGood)
        {
            caret = _lastGoodCaret;
            host = _lastGoodHost;
            hasCaret = true;
        }

        if (hasCaret)
        {
            int caretHeight = caret.Height;
            if (caretHeight <= 0) caretHeight = Height;
            x = caret.Left;
            y = caret.Top + caretHeight + 2;
            if (y + height > screenH) y = caret.Top - height - 2;
        }
        else if (host != IntPtr.Zero && GetClientRect(host, out var rc))
        {
            var origin = new POINT { X = rc.Right - MinWidth, Y = rc.Bottom - Height * 2 };
            ClientToScreen(host, ref origin);
            x = origin.X;
            y = origin.Y;
        }
        else
        {
            return;
        }

        x = Math.Max(0, Math.Min(x, screenW - width));
        y = Math.Max(0, Math.Min(y, screenH - height));

        if (x == _lastX && y == _lastY && width == _lastWidth && height == _lastHeight) return;
        _lastX = x; _lastY = y; _lastWidth = width; _lastHeight = height;

        SetWindowPos(_hwnd, HWND_TOPMOST, x, y, width, height, SWP_NOACTIVATE);
    }

    private int MeasureWidth()
    {
        IntPtr dc = CreateCompatibleDC(IntPtr.Zero);
        IntPtr hFont = CreateStatusFont();
        IntPtr oldFont = hFont != IntPtr.Zero ? SelectObject(dc, hFont) : IntPtr.Zero;

        int width = MinWidth;
        if (GetTextExtentPoint32W(dc, _label, _label.Length, out SIZE sz))
            width = Math.Max(MinWidth, PaddingX * 2 + sz.cx);

        if (oldFont != IntPtr.Zero) SelectObject(dc, oldFont);
        if (hFont != IntPtr.Zero) DeleteObject(hFont);
        DeleteDC(dc);

        int screenW = GetSystemMetrics(0);
        return Math.Min(width, Math.Max(MinWidth, screenW - 16));
    }

    private static IntPtr CreateStatusFont() => CreateFontW(
        FontHeight, 0, 0, 0, FwNormal, 0, 0, 0, DefaultCharset, 0, 0, 0, 0, FaceName);

    private static IntPtr OnWndProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        try
        {
            switch (msg)
            {
                case WM_DESTROY:
                    return IntPtr.Zero;

                case WM_PAINT:
                    Paint(hwnd);
                    ValidateRect(hwnd, IntPtr.Zero);
                    return IntPtr.Zero;

                case WM_ERASEBKGND:
                    return new IntPtr(1);

                default:
                    return DefWindowProc(hwnd, msg, wParam, lParam);
            }
        }
        catch (Exception ex)
        {
            TsfDebugLog.Write("StatusWnd", $"WndProc exception msg=0x{msg:X}: {ex.GetType().Name}: {ex.Message}");
            return IntPtr.Zero;
        }
    }

    private static void Paint(IntPtr hwnd)
    {
        var window = GetWindow(hwnd);
        if (window == null) return;

        IntPtr hdc = GetDC(hwnd);
        if (hdc == IntPtr.Zero) return;

        GetClientRect(hwnd, out var rc);
        int w = Math.Max(1, rc.Right - rc.Left);
        int h = Math.Max(1, rc.Bottom - rc.Top);

        IntPtr memDc = IntPtr.Zero;
        IntPtr memBmp = IntPtr.Zero;
        IntPtr oldBmp = IntPtr.Zero;
        IntPtr oldFont = IntPtr.Zero;
        IntPtr hFont = IntPtr.Zero;
        try
        {
            memDc = CreateCompatibleDC(hdc);
            memBmp = CreateCompatibleBitmap(hdc, w, h);
            oldBmp = SelectObject(memDc, memBmp);

            hFont = CreateStatusFont();
            oldFont = hFont != IntPtr.Zero ? SelectObject(memDc, hFont) : IntPtr.Zero;

            uint bg = window._english ? EnBg : ZhBg;
            uint text = window._english ? EnText : ZhText;

            var hBg = CreateSolidBrush(bg);
            var full = new RECT { Left = 0, Top = 0, Right = w, Bottom = h };
            FillRect(memDc, ref full, hBg);
            DeleteObject(hBg);

            SetBkMode(memDc, 1); // TRANSPARENT
            SetTextColor(memDc, text);

            var textRect = new RECT { Left = PaddingX, Top = 0, Right = w - PaddingX, Bottom = h };
            DrawText(memDc, window._label, window._label.Length, ref textRect, DT_CENTER | DT_SINGLELINE);

            BitBlt(hdc, 0, 0, w, h, memDc, 0, 0, SRCCOPY);
        }
        finally
        {
            if (oldFont != IntPtr.Zero) SelectObject(memDc, oldFont);
            if (hFont != IntPtr.Zero) DeleteObject(hFont);
            if (memBmp != IntPtr.Zero) SelectObject(memDc, oldBmp);
            if (memBmp != IntPtr.Zero) DeleteObject(memBmp);
            if (memDc != IntPtr.Zero) DeleteDC(memDc);
            ReleaseDC(hwnd, hdc);
        }
    }

    private static StatusIndicator? GetWindow(IntPtr hwnd) =>
        _instances.TryGetValue(hwnd, out var w) ? w : null;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_hwnd != IntPtr.Zero)
        {
            _instances.Remove(_hwnd);
            DestroyWindow(_hwnd);
            _hwnd = IntPtr.Zero;
            _visible = false;
        }
    }
}
