using System.Runtime.InteropServices;
using System.Windows;
using WpfClipboard = System.Windows.Clipboard;

namespace AiIme.Shell.Clipboard;

/// <summary>
/// 剪贴板 + 模拟粘贴实现：备份现有纯文本 -> 写入目标文本 ->
/// 把前台焦点窗口换回并模拟 Ctrl+V -> 粘贴后可恢复备份。
/// 不拦截全局键盘；在提权进程 / 安全控件内模拟粘贴可能失败，此时降级为仅写剪贴板。
/// </summary>
public sealed class ClipboardPoster : IClipboardPoster
{
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

    private const byte VK_CONTROL = 0x11;
    private const byte VK_V = 0x56;
    private const uint KEYUP = 0x0002;

    private IntPtr _previousForeground;

    public string? Backup()
    {
        try
        {
            return WpfClipboard.ContainsText() ? WpfClipboard.GetText() : null;
        }
        catch
        {
            return null;
        }
    }

    public void SetText(string text)
    {
        _previousForeground = GetForegroundWindow();
        try
        {
            WpfClipboard.SetText(text);
        }
        catch
        {
            // 剪贴板被占用时静默降级，后续可重试。
        }
    }

    public bool PasteToForeground()
    {
        if (_previousForeground == IntPtr.Zero) return false;

        // 归还焦点到上一个前台窗口，再模拟 Ctrl+V。
        SetForegroundWindow(_previousForeground);
        keybd_event(VK_CONTROL, 0, 0, UIntPtr.Zero);
        keybd_event(VK_V, 0, 0, UIntPtr.Zero);
        keybd_event(VK_V, 0, KEYUP, UIntPtr.Zero);
        keybd_event(VK_CONTROL, 0, KEYUP, UIntPtr.Zero);
        return true;
    }

    public void Restore(string? backup)
    {
        if (string.IsNullOrEmpty(backup)) return;
        try
        {
            WpfClipboard.SetText(backup);
        }
        catch
        {
            // 恢复失败不影响本次上屏。
        }
    }
}
