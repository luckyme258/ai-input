using System.IO;
using System.Text;

namespace AiIme.Tsf.TextService;

/// <summary>
/// TSF 真机联调用临时调试日志。默认关闭；设置环境变量 AIIME_TSF_DEBUG=1 后开启。
/// 日志追加到 %LOCALAPPDATA%\AiIme\logs\tsf-debug.log，便于在不挂调试器的情况下
/// 确认服务是否被激活、按键是否进入 sink、编辑会话是否提交。
///
/// 性能注意：每次按键会打多条日志，且每条都是一次同步文件写入（lock + AppendAllText）。
/// 开启后打字会出现明显卡顿，仅在排查问题时临时打开，验证完务必改回默认关闭。
///
/// 注意：这是联调辅助代码，上屏闭环稳定后可整体移除。
/// </summary>
internal static class TsfDebugLog
{
    private static readonly object Sync = new();
    // 只有显式设置 AIIME_TSF_DEBUG=1 才开启（默认关闭）。
    // 旧实现写成"不等于 0 即开启"，导致未设置变量时也默认开启，每键十余次磁盘写入，
    // 是输入卡顿的重要原因。
    private static readonly bool Enabled =
        string.Equals(Environment.GetEnvironmentVariable("AIIME_TSF_DEBUG"), "1", StringComparison.OrdinalIgnoreCase);

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
