using System.IO;
using System.Text;

namespace AiIme.Tsf.TextService;

/// <summary>
/// TSF 真机联调用临时调试日志。默认关闭；设置环境变量 AIIME_TSF_DEBUG=1 后开启。
/// 日志追加到 comhost 同目录 logs\tsf-debug.log，便于在不挂调试器的情况下
/// 确认服务是否被激活、按键是否进入 sink、编辑会话是否提交。
///
/// 注意：这是联调辅助代码，上屏闭环稳定后可整体移除。
/// </summary>
internal static class TsfDebugLog
{
    private static readonly object Sync = new();
    private static readonly bool Enabled =
        !string.Equals(Environment.GetEnvironmentVariable("AIIME_TSF_DEBUG"), "0", StringComparison.OrdinalIgnoreCase);

    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AiIme", "logs", "tsf-debug.log");

    public static bool IsEnabled => Enabled;

    public static void Write(string message)
    {
        if (!Enabled) return;
        try
        {
            lock (Sync)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
                var line = $"{DateTime.Now:HH:mm:ss.fff} [tid={Environment.CurrentManagedThreadId}] {message}{Environment.NewLine}";
                File.AppendAllText(LogPath, line, Encoding.UTF8);
            }
        }
        catch
        {
            // 日志失败绝不能影响输入法主流程。
        }
    }

    public static void Write(string tag, string message) => Write($"{tag}: {message}");
}
