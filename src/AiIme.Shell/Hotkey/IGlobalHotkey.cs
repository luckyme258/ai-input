namespace AiIme.Shell.Hotkey;

/// <summary>
/// 全局热键接口。实现基于 Win32 RegisterHotKey / WM_HOTKEY。
/// 把 UI 唤起逻辑与热键注册细节解耦，便于替换实现与单测。
/// </summary>
public interface IGlobalHotkey : IDisposable
{
    /// <summary>注册全局热键。返回是否注册成功（被占用/无效组合时为 false）。</summary>
    bool Register(HotkeyModifiers modifiers, int virtualKey);

    /// <summary>注销已注册的热键。</summary>
    void Unregister();

    /// <summary>热键触发时引发。</summary>
    event EventHandler? Pressed;
}
