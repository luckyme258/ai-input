namespace AiIme.Shell.Hotkey;

/// <summary>
/// 全局热键修饰键标志（对应 Win32 MOD_* 常量），可按位组合。
/// </summary>
[Flags]
public enum HotkeyModifiers
{
    /// <summary>无修饰键。</summary>
    None = 0,

    /// <summary>Alt (MOD_ALT)。</summary>
    Alt = 1,

    /// <summary>Ctrl (MOD_CONTROL)。</summary>
    Control = 2,

    /// <summary>Shift (MOD_SHIFT)。</summary>
    Shift = 4,

    /// <summary>Win 徽标键 (MOD_WIN)。</summary>
    Win = 8,

    /// <summary>不重复触发（MOD_NOREPEAT）：按住不放只触发一次。</summary>
    NoRepeat = 0x4000,
}
