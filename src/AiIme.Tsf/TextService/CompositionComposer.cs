using System.Runtime.InteropServices;
using AiIme.Core.Composition;
using AiIme.Tsf.Com;

namespace AiIme.Tsf.TextService;

/// <summary>
/// 上屏协调器：把纯托管的 <see cref="CompositionState"/> 内容，通过 TSF 编辑会话
/// 写回目标编辑框。隔离 COM 会话/指针逻辑，便于真机联调时单独修正。
///
/// 持有跨按键会话存活的当前 composition 引用（ITfComposition*），
/// 由编辑会话在 StartComposition / EndComposition 时更新。
/// </summary>
public sealed class CompositionComposer
{
    private readonly uint _clientId;
    private IntPtr _composition;

    public CompositionComposer(uint clientId)
    {
        _clientId = clientId;
    }

    /// <summary>当前是否存在进行中的 composition 区间。</summary>
    public bool HasComposition => _composition != IntPtr.Zero;

    /// <summary>记录新创建的 composition 引用（释放旧引用）。</summary>
    public void SetComposition(IntPtr pComposition)
    {
        if (_composition != IntPtr.Zero) Marshal.Release(_composition);
        _composition = pComposition;
    }

    /// <summary>读取当前 composition 引用（不转移所有权）。</summary>
    public bool TryGetComposition(out IntPtr pComposition)
    {
        pComposition = _composition;
        return _composition != IntPtr.Zero;
    }

    /// <summary>清空当前 composition 引用（释放）。</summary>
    public void ClearComposition()
    {
        if (_composition != IntPtr.Zero)
        {
            Marshal.Release(_composition);
            _composition = IntPtr.Zero;
        }
    }

    /// <summary>
    /// 按键回调后调用：把当前拼音串提交到目标上下文（累积阶段，写拼音串到 composition）。
    /// </summary>
    /// <param name="pic">OnKeyDown 传入的 ITfContext 原始指针。</param>
    /// <param name="composition">当前拼音状态机。</param>
    public void Commit(IntPtr pic, CompositionState composition)
    {
        CommitText(pic, composition.Pinyin, composition.Candidates.Count);
    }

    /// <summary>
    /// 提交一段指定文本到目标上下文（候选词上屏 / 清空等）。
    /// </summary>
    /// <param name="pic">OnKeyDown 传入的 ITfContext 原始指针。</param>
    /// <param name="text">要上屏的文本；空串表示结束并清空 composition。</param>
    /// <param name="candidateCount">当前候选数（仅用于日志）。</param>
    public void CommitText(IntPtr pic, string text, int candidateCount)
    {
        if (pic == IntPtr.Zero) return;

        var context = Marshal.GetObjectForIUnknown(pic) as ITfContext;
        if (context == null)
        {
            TsfDebugLog.Write("Commit", $"Marshal.GetObjectForIUnknown 未得到 ITfContext pic={pic}");
            return;
        }

        var session = new EditSession(context, text, this);
        var sessionPtr = Marshal.GetComInterfaceForObject(session, typeof(ITfEditSession));
        try
        {
            // 同步 + 读写会话。phrsession 为会话句柄，当前骨架不使用。
            int hr = context.RequestEditSession(
                _clientId,
                sessionPtr,
                TsfGuids.TF_ES_SYNC | TsfGuids.TF_ES_READWRITE,
                out _);
            TsfDebugLog.Write("Commit",
                $"RequestEditSession HRESULT=0x{hr:X8} text='{text}' candidates={candidateCount}");
        }
        finally
        {
            Marshal.Release(sessionPtr);
        }
    }
}
