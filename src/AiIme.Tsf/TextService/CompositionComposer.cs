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
    private readonly CompositionSink _sink;
    private readonly IntPtr _sinkPtr;

    /// <summary>当前文本服务的 TSF 客户端标识。</summary>
    public uint ClientId => _clientId;

    /// <summary>传给 StartComposition 的 ITfCompositionSink 接口指针（生命周期随协调器）。</summary>
    public IntPtr CompositionSinkPtr => _sinkPtr;

    public CompositionComposer(uint clientId)
    {
        _clientId = clientId;
        _sink = new CompositionSink(this);
        _sinkPtr = Marshal.GetComInterfaceForObject(_sink, typeof(ITfCompositionSink));
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

    /// <summary>判断回调里被终止的区间是否就是当前持有区间（防止把正常结束误判为外部终止）。</summary>
    public bool IsCurrentComposition(IntPtr pComposition) =>
        _composition != IntPtr.Zero && _composition == pComposition;

    /// <summary>清空当前 composition 引用（释放）。</summary>
    public void ClearComposition()
    {
        if (_composition != IntPtr.Zero)
        {
            Marshal.Release(_composition);
            _composition = IntPtr.Zero;
        }
    }

    /// <summary>释放 sink 接口指针（服务停用调用）。</summary>
    public void Dispose()
    {
        ClearComposition();
        if (_sinkPtr != IntPtr.Zero)
        {
            Marshal.Release(_sinkPtr);
        }
    }

    /// <summary>
    /// 按键回调后调用：把当前拼音串提交到目标上下文（累积阶段，写拼音串到 composition）。
    /// </summary>
    /// <param name="pic">OnKeyDown 传入的 ITfContext 原始指针。</param>
    /// <param name="composition">当前拼音状态机。</param>
    public void Commit(IntPtr pic, CompositionState composition)
    {
        CommitText(pic, composition.Pinyin, composition.Candidates.Count, finalCommit: false);
    }

    /// <summary>
    /// 提交一段指定文本到目标上下文（候选词上屏 / 清空等）。
    /// </summary>
    /// <param name="pic">OnKeyDown 传入的 ITfContext 原始指针。</param>
    /// <param name="text">要上屏的文本；空串表示结束并清空 composition。</param>
    /// <param name="candidateCount">当前候选数（仅用于日志）。</param>
    /// <param name="finalCommit">true 表示这是最终上屏（选词/回车），写入后结束 composition 区间。</param>
    public void CommitText(IntPtr pic, string text, int candidateCount, bool finalCommit = false)
    {
        if (pic == IntPtr.Zero) return;

        var context = Marshal.GetObjectForIUnknown(pic) as ITfContext;
        if (context == null)
        {
            TsfDebugLog.Write("Commit", $"Marshal.GetObjectForIUnknown 未得到 ITfContext pic={pic}");
            return;
        }

        // 空串：结束并清空当前区间。
        if (text.Length == 0)
        {
            RunSession(context, SessionMode.Abort, string.Empty);
            return;
        }

        // 独立会话先建区间（对齐 Weasel：建区间与写文本分开）。
        if (!HasComposition)
        {
            RunSession(context, SessionMode.Ensure, string.Empty);
        }

        RunSession(context, finalCommit ? SessionMode.Commit : SessionMode.Preedit, text);
        TsfDebugLog.Write("Commit", $"dispatched text='{text}' candidates={candidateCount} final={finalCommit}");
    }

    /// <summary>在一个同步读写编辑会话内执行指定模式。</summary>
    private void RunSession(ITfContext context, SessionMode mode, string text)
    {
        var session = new EditSession(context, this, mode, text);
        var sessionPtr = Marshal.GetComInterfaceForObject(session, typeof(ITfEditSession));
        try
        {
            // 同步 + 读写会话（异步 ASYNCDONTCARE 在本机 comhost 下会话未能存活到回调，回退 SYNC）。
            int hr = context.RequestEditSession(
                _clientId,
                sessionPtr,
                TsfGuids.TF_ES_SYNC | TsfGuids.TF_ES_READWRITE,
                out _);
            TsfDebugLog.Write("Commit", $"RequestEditSession mode={mode} HRESULT=0x{hr:X8}");
        }
        finally
        {
            Marshal.Release(sessionPtr);
        }
    }
}
