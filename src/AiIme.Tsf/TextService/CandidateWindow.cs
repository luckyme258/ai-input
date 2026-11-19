using System.Runtime.InteropServices;
using System.Text;
using AiIme.Core.Models;
using AiIme.Tsf.Com;

namespace AiIme.Tsf.TextService;

/// <summary>
/// 自绘候选窗：原生 Win32 置顶弹出窗，用 GDI 列出当前候选词，高亮首项。
/// 不依赖 WPF/桌面运行时，避免 comhost 进程额外拉取桌面框架。
///
/// 线程约定：TSF 按键回调在宿主进程线程内执行；窗口创建/消息处理都在该线程，
/// 不另起消息循环——本窗只做静态展示，不处理交互消息（选词由 sink 按键完成）。
/// </summary>
internal sealed class CandidateWindow : IDisposable
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

    private const int COLOR_WINDOW = 5;
    private const int COLOR_WINDOWTEXT = 8;
    private const int COLOR_HIGHLIGHT = 13;
    private const int COLOR_HIGHLIGHTTEXT = 14;

    private const int RowHeight = 22;
    private const int MaxRows = 9;
    private const int MarginX = 10;
    private const int PaddingX = 8;

    // 候选窗配色写死（浅色弹出窗），避免依赖系统主题色 / GetSysColor 编组带来的歧义。
    // COLORREF 为 0x00BBGGRR。
    private const uint NormalBg = 0x00FFFFFF;      // 白
    private const uint NormalText = 0x00000000;     // 黑
    private const uint SelBg = 0x00D77800;          // 蓝 RGB(0,120,215)
    private const uint SelText = 0x00FFFFFF;       // 白

    private static readonly WndProcDelegate WndProc = OnWndProc;
    private static readonly StringBuilder ClassName = new("AiImeCandidateWnd");

    private IntPtr _hwnd;
    private bool _classRegistered;
    private bool _visible;
    private int _lastX = int.MinValue;
    private int _lastY = int.MinValue;
    private int _lastWidth;
    private int _lastHeight;
    private IReadOnlyList<Candidate> _candidates = Array.Empty<Candidate>();
    private int _selected;
    private bool _disposed;

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
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
    private static extern IntPtr GetSysColor(int nIndex);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateSolidBrush(uint crColor);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr ho);

    [DllImport("gdi32.dll")]
    private static extern IntPtr GetStockObject(int nIndex);

    // DEFAULT_GUI_FONT 库存对象：随系统自带中文字形回退，避免默认 DC 字体缺 CJK 字形。
    private const int DEFAULT_GUI_FONT = 17;

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

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    private static readonly IntPtr HWND_TOPMOST = new(-1);
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const int SW_SHOW = 5;
    private const int SW_HIDE = 0;
    private const uint SRCCOPY = 0x00CC0020;

    private string _lastSignature = string.Empty;
    private IntPtr _lastGoodHost;
    private TfRect _lastGoodCaret;
    private bool _hasLastGood;

    /// <summary>候选窗自身句柄（用于把误判为宿主的自指句柄排除）。</summary>
    public IntPtr Hwnd => _hwnd;

    /// <summary>更新候选并在光标附近显示。无候选时隐藏。</summary>
    public void Update(IReadOnlyList<Candidate> candidates, int selected, IntPtr parentHwnd, TfRect caret)
    {
        if (_disposed) return;

        _candidates = candidates ?? Array.Empty<Candidate>();
        _selected = selected;

        var top = string.Join(",", _candidates.Take(3).Select(c => c.Text));
        TsfDebugLog.Write("CandidateWnd", $"update count={_candidates.Count} top=[{top}]");

        if (_candidates.Count == 0)
        {
            Hide();
            return;
        }

        EnsureWindow(parentHwnd == _hwnd ? IntPtr.Zero : parentHwnd);
        if (_hwnd == IntPtr.Zero) return;

        PositionNear(parentHwnd == _hwnd ? IntPtr.Zero : parentHwnd, caret);
        if (!_visible)
        {
            ShowWindow(_hwnd, SW_SHOW);
            _visible = true;
        }

        // 候选内容未变化时不触发重绘，避免每按键无效刷新引起闪烁。
        var signature = BuildSignature();
        if (signature != _lastSignature)
        {
            _lastSignature = signature;
            InvalidateRect(_hwnd, IntPtr.Zero, false);
            // 本线程不挂独立消息循环，WM_PAINT 不一定被及时派发；直接同步画一帧，保证首屏立即可见文字。
            Paint(_hwnd);
        }
    }

    private string BuildSignature()
    {
        var sb = new StringBuilder();
        sb.Append(_selected).Append('|');
        foreach (var c in _candidates.Take(MaxRows)) sb.Append(c.Text).Append(',');
        return sb.ToString();
    }

    /// <summary>隐藏候选窗。</summary>
    public void Hide()
    {
        if (_hwnd != IntPtr.Zero && _visible)
        {
            ShowWindow(_hwnd, SW_HIDE);
            _visible = false;
            _lastX = int.MinValue;
            _lastY = int.MinValue;
            _lastSignature = string.Empty;
        }
    }

    private void EnsureWindow(IntPtr parentHwnd)
    {
        if (_hwnd != IntPtr.Zero) return;

        if (!_classRegistered)
        {
            var wc = new WNDCLASSEX
            {
                cbSize = Marshal.SizeOf<WNDCLASSEX>(),
                lpfnWndProc = WndProc,
                // 不设类背景刷：背景由 WM_PAINT 双缓冲统一绘制，避免系统先擦除产生闪烁。
                hbrBackground = IntPtr.Zero,
                lpszClassName = ClassName.ToString(),
            };
            if (RegisterClassEx(ref wc) != 0) _classRegistered = true;
        }

        _hwnd = CreateWindowEx(
            WS_EX_TOPMOST | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE,
            ClassName.ToString(), string.Empty, WS_POPUP,
            0, 0, 200, RowHeight * Math.Min(_candidates.Count, MaxRows),
            IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);

        if (_hwnd != IntPtr.Zero) RegisterInstance();
        TsfDebugLog.Write("CandidateWnd", $"CreateWindow hwnd={_hwnd}");
    }

    private void PositionNear(IntPtr parentHwnd, TfRect caret)
    {
        var rows = Math.Min(_candidates.Count, MaxRows);
        int height = RowHeight * rows;
        int width = MeasureWidth();

        int screenW = GetSystemMetrics(0);
        int screenH = GetSystemMetrics(1);

        IntPtr host = parentHwnd;
        int x, y;

        // 退化/角落矩形（空文档折叠光标常落在屏幕右下角）判为无效；无效时沿用最近一次有效位置。
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
            // 主路径：贴在光标下方；下方放不下则翻到光标上方。
            int caretHeight = caret.Height;
            if (caretHeight <= 0) caretHeight = RowHeight; // 折叠光标高度为 0，按默认行高估算。
            x = caret.Left;
            y = caret.Top + caretHeight + 2;
            if (y + height > screenH) y = caret.Top - height - 2;
        }
        else if (host != IntPtr.Zero && GetClientRect(host, out var rc))
        {
            // 兜底：取不到光标矩形时，放在父窗口右下。
            var origin = new POINT { X = rc.Right - 220, Y = rc.Bottom - RowHeight * 2 };
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

        // 位置/尺寸未变时不重复 SetWindowPos，避免每帧重排引起闪烁。
        if (x == _lastX && y == _lastY && width == _lastWidth && height == _lastHeight) return;
        _lastX = x; _lastY = y; _lastWidth = width; _lastHeight = height;

        TsfDebugLog.Write("CandidateWnd", $"pos caret=({caret.Left},{caret.Top},{caret.Right},{caret.Bottom}) -> x={x} y={y} w={width} h={height}");
        SetWindowPos(_hwnd, HWND_TOPMOST, x, y, width, height, SWP_NOACTIVATE);
    }

    private int MeasureWidth()
    {
        int max = 80;
        foreach (var c in _candidates.Take(MaxRows))
        {
            // 序号 + 分隔 + 词；粗略按字符宽度估算。
            max = Math.Max(max, PaddingX * 2 + c.Text.Length * 16);
        }
        return max + MarginX;
    }

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
                    // 校验并清空更新区，否则更新区持续为脏，WM_PAINT 会被无限重投形成刷屏循环。
                    ValidateRect(hwnd, IntPtr.Zero);
                    return IntPtr.Zero;

                case WM_ERASEBKGND:
                    // 由 WM_PAINT 统一填充，避免闪烁。
                    return new IntPtr(1);

                default:
                    return DefWindowProc(hwnd, msg, wParam, lParam);
            }
        }
        catch (Exception ex)
        {
            TsfDebugLog.Write("CandidateWnd", $"WndProc exception msg=0x{msg:X}: {ex.GetType().Name}: {ex.Message}");
            return IntPtr.Zero;
        }
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr DefWindowProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

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
        try
        {
            memDc = CreateCompatibleDC(hdc);
            memBmp = CreateCompatibleBitmap(hdc, w, h);
            oldBmp = SelectObject(memDc, memBmp);

            // 选中系统 GUI 字体（含中文字形回退），用完在 finally 恢复。
            IntPtr hFont = GetStockObject(DEFAULT_GUI_FONT);
            oldFont = hFont != IntPtr.Zero ? SelectObject(memDc, hFont) : IntPtr.Zero;

            // 背景先画到内存 DC，避免直接在屏幕上分块擦除/填充产生闪烁。
            var hBg = CreateSolidBrush(NormalBg);
            var full = new RECT { Left = 0, Top = 0, Right = w, Bottom = h };
            bool bgOk = FillRect(memDc, ref full, hBg);
            DeleteObject(hBg);
            SetBkMode(memDc, 1); // TRANSPARENT：文字背景不擦除，避免盖住高亮底色。

            int rows = Math.Min(window._candidates.Count, MaxRows);
            for (int i = 0; i < rows; i++)
            {
                var rowRect = new RECT { Left = 0, Top = i * RowHeight, Right = w, Bottom = (i + 1) * RowHeight };
                bool selected = i == window._selected;

                IntPtr hSel = IntPtr.Zero;
                bool selOk = true;
                if (selected)
                {
                    hSel = CreateSolidBrush(SelBg);
                    selOk = FillRect(memDc, ref rowRect, hSel);
                    DeleteObject(hSel);
                }

                var textRect = new RECT { Left = PaddingX, Top = rowRect.Top, Right = rowRect.Right - PaddingX, Bottom = rowRect.Bottom };
                string label = $"{i + 1}. {window._candidates[i].Text}";

                uint textColor = selected ? SelText : NormalText;
                SetTextColor(memDc, textColor);

                if (i < 2)
                {
                    var codes = string.Join(" ", label.Select(ch => ((int)ch).ToString("X4")));
                    TsfDebugLog.Write("CandidateWnd",
                        $"Paint row{i} sel={selected} label='{label}' codes=[{codes}] rect=({textRect.Left},{textRect.Top},{textRect.Right},{textRect.Bottom}) color=0x{textColor:X8} hSel={hSel} selOk={selOk} bgOk={bgOk} win={w}x{h}");
                }

                DrawText(memDc, label, label.Length, ref textRect, DT_LEFT | DT_SINGLELINE);
            }

            BitBlt(hdc, 0, 0, w, h, memDc, 0, 0, SRCCOPY);
        }
        finally
        {
            if (oldFont != IntPtr.Zero) SelectObject(memDc, oldFont);
            if (memBmp != IntPtr.Zero) SelectObject(memDc, oldBmp);
            if (memBmp != IntPtr.Zero) DeleteObject(memBmp);
            if (memDc != IntPtr.Zero) DeleteDC(memDc);
            ReleaseDC(hwnd, hdc);
        }
    }

    [DllImport("gdi32.dll")]
    private static extern int SetTextColor(IntPtr hdc, uint crColor);

    [DllImport("gdi32.dll")]
    private static extern int SetBkMode(IntPtr hdc, int mode);

    private static uint ColorRef(IntPtr sysColor) => unchecked((uint)sysColor.ToInt64());

    private static CandidateWindow? GetWindow(IntPtr hwnd) => _instances.TryGetValue(hwnd, out var w) ? w : null;

    private static readonly Dictionary<IntPtr, CandidateWindow> _instances = new();

    private void RegisterInstance() => _instances[_hwnd] = this;

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
