namespace AiIme.Shell.Clipboard;

/// <summary>
/// 上屏通道：把拼好的句子写入剪贴板，并（可选）对当前焦点窗口模拟一次粘贴。
/// 不拦截全局键盘、不注册 TSF；提权进程/安全控件内模拟粘贴可能失败，
/// 此时降级为仅写剪贴板，由用户手动 Ctrl+V。
/// </summary>
public interface IClipboardPoster
{
    /// <summary>备份当前剪贴板纯文本（用于粘贴后恢复）。</summary>
    string? Backup();

    /// <summary>写入文本到剪贴板。</summary>
    void SetText(string text);

    /// <summary>把焦点归还上一个前台窗口并模拟 Ctrl+V；返回是否成功发送。</summary>
    bool PasteToForeground();

    /// <summary>恢复备份的剪贴板内容。</summary>
    void Restore(string? backup);
}
