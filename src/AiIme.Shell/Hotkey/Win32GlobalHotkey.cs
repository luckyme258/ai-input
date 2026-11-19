using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace AiIme.Shell.Hotkey;

/// <summary>
/// 基于 Win32 RegisterHotKey / UnregisterHotKey 的全局热键实现。
/// 通过一个隐藏 HwndSource 消息窗口接收 WM_HOTKEY，把消息转成 <see cref="Pressed"/> 事件。
/// 必须在创建过 WPF 消息循环的线程（UI 线程）上注册与注销。
/// </summary>
public sealed class Win32GlobalHotkey : IGlobalHotkey
{
    private const int WM_HOTKEY = 0x0312;
    private const int HK_ID = 0xA11E;

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private HwndSource? _source;
    private bool _registered;
    private HotkeyModifiers _modifiers;
    private int _virtualKey;

    public event EventHandler? Pressed;

    public bool Register(HotkeyModifiers modifiers, int virtualKey)
    {
        Unregister();

        _modifiers = modifiers;
        _virtualKey = virtualKey;

        // 隐藏消息窗口：仅用于接收 WM_HOTKEY，不显示在任务栏/屏幕。
        var parameters = new HwndSourceParameters("AiImeHotkeyMessageWindow")
        {
            Width = 0,
            Height = 0,
            WindowStyle = 0,
            ParentWindow = new IntPtr(-3), // HWND_MESSAGE
        };
        _source = new HwndSource(parameters);
        _source.AddHook(WndProc);

        var ok = RegisterHotKey(_source.Handle, HK_ID, (uint)modifiers, (uint)virtualKey);
        if (ok)
        {
            _registered = true;
        }
        else
        {
            // 注册失败（热键被占用等）：释放消息窗口。
            Unregister();
        }

        return ok;
    }

    public void Unregister()
    {
        if (_registered && _source != null)
        {
            UnregisterHotKey(_source.Handle, HK_ID);
        }
        _registered = false;

        if (_source != null)
        {
            _source.RemoveHook(WndProc);
            _source.Dispose();
            _source = null;
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY && wParam.ToInt32() == HK_ID)
        {
            Pressed?.Invoke(this, EventArgs.Empty);
            handled = true;
        }
        return IntPtr.Zero;
    }

    public void Dispose() => Unregister();
}
