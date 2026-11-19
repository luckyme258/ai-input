using System.Windows;
using AiIme.Core.Dictionary;
using AiIme.Core.Learning;
using AiIme.Core.Ranking;
using AiIme.Core.Store;
using AiIme.Shell.Clipboard;
using AiIme.Shell.Hotkey;

namespace AiIme.Shell;

/// <summary>
/// 应用启动入口：组装 Core 层依赖（存储 / 学习 / 排序 / 词典）并挂载主候选窗。
/// 辅助工具形态：自己窗口拼句 -> 选词上屏 -> 剪贴板自动粘贴；全局热键仅用于唤起本窗口，
/// 不拦截、不改写系统输入链路。
/// </summary>
public partial class App : Application
{
    // 默认唤起热键：Ctrl + 空格（VK_SPACE = 0x20）。后续可挪到配置文件。
    private const HotkeyModifiers InvokeModifiers = HotkeyModifiers.Control;
    private const int InvokeVirtualKey = 0x20;

    private SqliteProfileStore? _store;
    private IGlobalHotkey? _hotkey;

    private void OnStartup(object sender, StartupEventArgs e)
    {
        _store = new SqliteProfileStore();

        var dictionary = new TrieDictionary(DictPaths.ResolveDefaultDirectory());
        dictionary.Load();

        var learner = new HabitLearner(_store);
        var ranker = new CandidateRanker(_store);
        var poster = new ClipboardPoster();

        var mainWindow = new MainWindow(dictionary, learner, ranker, poster, _store)
        {
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
        };

        _hotkey = new Win32GlobalHotkey();
        _hotkey.Pressed += (_, _) => mainWindow.ActivateForInput();
        if (!_hotkey.Register(InvokeModifiers, InvokeVirtualKey))
        {
            // 热键被占用时不阻断启动，仅记录；窗口仍可手动打开。
            Console.WriteLine("全局热键注册失败（可能被其他程序占用），已跳过热键唤起。");
        }

        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _hotkey?.Dispose();
        _store?.Dispose();
        base.OnExit(e);
    }
}
