using System.Runtime.InteropServices;
using AiIme.Core.Composition;
using AiIme.Core.Learning;
using AiIme.Core.Models;
using AiIme.Tsf.Com;

namespace AiIme.Tsf.TextService;

/// <summary>
/// TSF 按键事件接收器。COM 回调在此实现，按键分流委托给纯托管的
/// <see cref="CompositionState"/>；变动后通过 <see cref="CompositionComposer"/>
/// 把文本提交到目标上下文。
///
/// 交互规则（第一版）：
/// - 字母：累积拼音，累积阶段把拼音串写进 composition；
/// - 回车：取首选候选词上屏（无候选则上屏拼音串）；
/// - 数字 1..9：选对应序号候选词上屏；
/// - Esc：清空 composition 与状态机；
/// - 退格：删一个拼音字母；
/// - 候选词上屏后调 <see cref="HabitLearner.RecordCommit"/> 回写学习。
/// </summary>
[ClassInterface(ClassInterfaceType.None)]
public sealed class KeyEventSink : ITfKeyEventSink
{
    private readonly CompositionState _composition;
    private readonly CompositionComposer _composer;
    private readonly HabitLearner _learner;
    private readonly CandidateWindow _candidateWindow;
    private readonly uint _clientId;

    /// <summary>最近已上屏词序列（用于 n-gram 上下文）。</summary>
    private readonly List<string> _committedWords = new();

    /// <summary>最近一次按键携带的 ITfContext 指针（焦点丢失结束 composition 时复用）。</summary>
    private IntPtr _lastPic;

    public KeyEventSink(CompositionState composition, CompositionComposer composer, HabitLearner learner, uint clientId)
    {
        _composition = composition ?? throw new ArgumentNullException(nameof(composition));
        _composer = composer ?? throw new ArgumentNullException(nameof(composer));
        _learner = learner ?? throw new ArgumentNullException(nameof(learner));
        _clientId = clientId;
        _candidateWindow = new CandidateWindow();
    }

    /// <summary>释放候选窗资源（服务停用调用）。</summary>
    internal void DisposeCandidateWindow() => _candidateWindow.Dispose();

    public int OnSetFocus(int fforeground)
    {
        TsfDebugLog.Write("OnSetFocus", $"ffg={fforeground}");
        // 焦点离开：取消尚未上屏的本地输入，结束残留 composition 区间，隐藏候选窗。
        if (fforeground == 0 && _composition.IsComposing)
        {
            _composition.Clear();
            _candidateWindow.Hide();
            if (_lastPic != IntPtr.Zero) _composer.CommitText(_lastPic, string.Empty, 0);
        }
        return HResult.S_OK;
    }

    public int OnTestKeyDown(IntPtr pic, IntPtr wparam, IntPtr lparam, out int pfEaten)
    {
        // 探测阶段：字母、退格、Esc、回车、数字由本输入法接管（仅在有 composition 时）。
        pfEaten = Consumes((int)wparam) ? 1 : 0;
        TsfDebugLog.Write("OnTestKeyDown", $"vk=0x{(int)wparam:X2} eat={pfEaten}");
        return HResult.S_OK;
    }

    public int OnTestKeyUp(IntPtr pic, IntPtr wparam, IntPtr lparam, out int pfEaten)
    {
        pfEaten = 0;
        TsfDebugLog.Write("OnTestKeyUp", $"vk=0x{(int)wparam:X2}");
        return HResult.S_OK;
    }

    public int OnKeyDown(IntPtr pic, IntPtr wparam, IntPtr lparam, out int pfEaten)
    {
        _lastPic = pic;
        TsfDebugLog.Write("OnKeyDown", $"entry vk=0x{(int)wparam:X2}");
        try
        {
            int vk = (int)wparam;
            bool eaten;
            string action;

            switch (vk)
            {
                case 0x08: // VK_BACK
                    eaten = _composition.Backspace();
                    action = "Backspace";
                    if (eaten) _composer.Commit(pic, _composition);
                    break;

                case 0x1B: // VK_ESCAPE
                    _composition.Clear();
                    eaten = true;
                    action = "Escape";
                    _composer.CommitText(pic, string.Empty, 0);
                    break;

                case 0x0D: // VK_RETURN
                case 0x20: // VK_SPACE：空格与回车一致，提交首选候选
                    eaten = _composition.IsComposing;
                    action = vk == 0x20 ? "Space" : "Enter";
                    if (eaten) CommitCandidate(pic, 0, correction: false);
                    break;

                default:
                    // 数字 1..9 选词（VK 0x31..0x39）。
                    if (vk is >= 0x31 and <= 0x39)
                    {
                        int index = vk - 0x31;
                        eaten = index < _composition.Candidates.Count;
                        action = $"Digit-{index + 1}";
                        if (eaten) CommitCandidate(pic, index, correction: index > 0);
                        break;
                    }

                    // 字母键：由状态机按 VK 转字符后累积。
                    eaten = _composition.Append(VkToChar(vk));
                    action = $"Key 0x{vk:X2}";
                    if (eaten) _composer.Commit(pic, _composition);
                    break;
            }

            TsfDebugLog.Write("OnKeyDown",
                $"{action} eaten={eaten} pic={pic} pinyin='{_composition.Pinyin}' candidates={_composition.Candidates.Count}");

            RefreshCandidateWindow(pic);
            pfEaten = eaten ? 1 : 0;
            return HResult.S_OK;
        }
        catch (Exception ex)
        {
            TsfDebugLog.Write("OnKeyDown", $"EXCEPTION: {ex.GetType().FullName}: {ex.Message}\n{ex.StackTrace}");
            pfEaten = 0;
            return HResult.S_OK;
        }
    }

    /// <summary>按当前状态机同步候选窗：有候选则在光标附近显示，否则隐藏。</summary>
    private void RefreshCandidateWindow(IntPtr pic)
    {
        var pos = GetCaretInfo(pic);
        if (_composition.IsComposing && _composition.Candidates.Count > 0)
        {
            _candidateWindow.Update(_composition.Candidates, selected: 0, pos.Hwnd, pos.Rect);
        }
        else
        {
            _candidateWindow.Hide();
        }
    }

    /// <summary>一次取回宿主窗口句柄与光标屏幕矩形；取不到则字段为零/空。</summary>
    private PositionInfo GetCaretInfo(IntPtr pic)
    {
        var info = new PositionInfo();
        if (pic == IntPtr.Zero)
        {
            TsfDebugLog.Write("GetCaretInfo", "pic=Zero");
            return info;
        }

        ITfContext? context = null;
        ITfContextView? view = null;
        try
        {
            context = Marshal.GetObjectForIUnknown(pic) as ITfContext;
            if (context == null)
            {
                TsfDebugLog.Write("GetCaretInfo", "QI ITfContext 返回 null");
                return info;
            }

            int hrView = context.GetActiveView(out var pView);
            TsfDebugLog.Write("GetCaretInfo", $"GetActiveView hr=0x{hrView:X8} pView={pView}");
            if (hrView != HResult.S_OK || pView == IntPtr.Zero) return info;

            try
            {
                view = (ITfContextView)Marshal.GetObjectForIUnknown(pView);

                int hrWnd = view.GetWnd(out var hwnd);
                info.Hwnd = hrWnd == HResult.S_OK ? hwnd : IntPtr.Zero;
                TsfDebugLog.Write("GetCaretInfo", $"GetWnd hr=0x{hrWnd:X8} hwnd={info.Hwnd}");

                // 只读会话内取选区并读光标矩形。
                var session = new CaretRectSession(context, view);
                var sessionPtr = Marshal.GetComInterfaceForObject(session, typeof(ITfEditSession));
                try
                {
                    int hr = context.RequestEditSession(
                        _clientId, sessionPtr, TsfGuids.TF_ES_SYNC | TsfGuids.TF_ES_READ, out _);
                    var r = session.Rect;
                    TsfDebugLog.Write("GetCaretInfo", $"rect session hr=0x{hr:X8} ok={session.Succeeded} L={r.Left} T={r.Top} R={r.Right} B={r.Bottom}");
                    if (session.Succeeded) info.Rect = session.Rect;
                }
                finally
                {
                    Marshal.Release(sessionPtr);
                }
            }
            finally
            {
                Marshal.Release(pView);
            }
        }
        catch (Exception ex)
        {
            TsfDebugLog.Write("GetCaretInfo", $"exception: {ex.GetType().Name}: {ex.Message}");
        }
        return info;
    }

    private sealed class PositionInfo
    {
        public IntPtr Hwnd;
        public TfRect Rect;
    }

    public int OnKeyUp(IntPtr pic, IntPtr wparam, IntPtr lparam, out int pfEaten)
    {
        TsfDebugLog.Write("OnKeyUp", $"vk=0x{(int)wparam:X2}");
        pfEaten = 0;
        return HResult.S_OK;
    }

    public int OnPreservedKey(IntPtr pic, ref Guid rguid, out int pfEaten)
    {
        TsfDebugLog.Write("OnPreservedKey", $"guid={rguid}");
        pfEaten = 0;
        return HResult.S_OK;
    }

    /// <summary>选定序号候选词上屏：写词文本、记录学习、清空状态机。</summary>
    private void CommitCandidate(IntPtr pic, int index, bool correction)
    {
        var candidates = _composition.Candidates;
        string word;
        bool pickedCandidate = index < candidates.Count;

        if (pickedCandidate)
        {
            word = candidates[index].Text;
        }
        else
        {
            // 无候选：上屏当前拼音串。
            word = _composition.Pinyin;
        }

        if (!string.IsNullOrEmpty(word))
        {
            var context = new InputContext { PreviousWords = _committedWords };
            _learner.RecordCommit(word, context, wasCorrection: correction);
            _committedWords.Add(word);

            _composer.CommitText(pic, word, candidates.Count, finalCommit: true);
        }

        _composition.Clear();
        _candidateWindow.Hide();
    }

    private static bool Consumes(int vk) => vk switch
    {
        0x08 or 0x1B or 0x0D or 0x20 => true,
        >= 0x31 and <= 0x39 => true,
        _ => IsLetterVk(vk),
    };

    private static bool IsLetterVk(int vk) => vk is >= 0x41 and <= 0x5A;

    private static char VkToChar(int vk) => IsLetterVk(vk) ? (char)vk : '\0';
}
