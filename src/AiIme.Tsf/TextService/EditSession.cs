using System.Runtime.InteropServices;
using AiIme.Tsf.Com;

namespace AiIme.Tsf.TextService;

/// <summary>
/// 编辑会话模式，对齐 Weasel 的独立会话拆分：
/// - <see cref="Ensure"/>：独立会话建 composition 区间（QUERYONLY 取落点 → StartComposition → 光标置末），不写文本；
/// - <see cref="Preedit"/>：复用当前区间，把拼音串写入 composition 覆盖范围；
/// - <see cref="Commit"/>：复用当前区间写入最终候选词，随后 EndComposition 固化为正文；
/// - <see cref="Abort"/>：结束并清空当前区间（退格删空 / Esc / 失焦）。
/// </summary>
public enum SessionMode
{
    Ensure,
    Preedit,
    Commit,
    Abort,
}

/// <summary>
/// 一次性编辑会话：在 TSF 发放写 cookie 的会话内维护 composition 区间并读写文本。
/// 由 <see cref="CompositionComposer"/> 通过 ITfContext.RequestEditSession 提交；
/// 当前 composition 引用由协调器跨会话持有。
/// </summary>
[ClassInterface(ClassInterfaceType.None)]
public sealed class EditSession : ITfEditSession
{
    private readonly ITfContext _context;
    private readonly CompositionComposer _owner;
    private readonly SessionMode _mode;
    private readonly string _text;

    public EditSession(ITfContext context, CompositionComposer owner, SessionMode mode, string text = "")
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        _mode = mode;
        _text = text ?? string.Empty;
    }

    public int DoEditSession(uint ec)
    {
        TsfDebugLog.Write("EditSession", $"DoEditSession mode={_mode} ec={ec} text='{_text}' composing={_owner.HasComposition}");

        IntPtr pRange = IntPtr.Zero;
        IntPtr pchText = IntPtr.Zero;
        IntPtr compRange = IntPtr.Zero;
        try
        {
            // 1. 取当前选区（只取首个选区）。
            int hrSel = _context.GetSelection(ec, 0, 1, out var selection, out uint fetched);
            if (hrSel != HResult.S_OK)
            {
                TsfDebugLog.Write("EditSession", $"GetSelection HRESULT=0x{hrSel:X8}");
                return hrSel;
            }

            pRange = selection.Range;
            TsfDebugLog.Write("EditSession", $"GetSelection fetched={fetched} range={pRange}");

            int hrW = _context.InWriteSession(_owner.ClientId, out int writeSession);
            TsfDebugLog.Write("EditSession", $"InWriteSession hr=0x{hrW:X8} write={writeSession}");

            if (pRange == IntPtr.Zero)
            {
                return HResult.S_OK;
            }

            switch (_mode)
            {
                case SessionMode.Abort:
                    EndCurrentComposition(ec);
                    return HResult.S_OK;

                case SessionMode.Ensure:
                    StartComposition(ec, pRange);
                    return HResult.S_OK;

                case SessionMode.Preedit:
                    // 累积阶段先不把拼音写进 composition 区间：实测对已含 interim 文本的区间
                    // 做替换写入在结束区间后会被宿主丢弃；保持区间为空，提交时再写入候选词。
                    if (!_owner.HasComposition) StartComposition(ec, pRange);
                    return HResult.S_OK;

                case SessionMode.Commit:
                    break;

                default:
                    return HResult.E_INVALIDARG;
            }

            pchText = Marshal.StringToHGlobalUni(_text);

            // 取 composition 自身覆盖范围（随插入文本扩展）；取不到回退选区范围。
            IntPtr writeRange = pRange;
            if (_owner.TryGetComposition(out var pComp))
            {
                try
                {
                    var comp = (ITfComposition)Marshal.GetObjectForIUnknown(pComp);
                    int hrGr = comp.GetRange(out compRange);
                    TsfDebugLog.Write("EditSession", $"Composition.GetRange hr=0x{hrGr:X8} range={compRange}");
                    if (hrGr == HResult.S_OK && compRange != IntPtr.Zero) writeRange = compRange;
                }
                catch (Exception exGr)
                {
                    TsfDebugLog.Write("EditSession", $"Composition.GetRange 异常: {exGr.Message}");
                }
            }

            if (!(Marshal.GetObjectForIUnknown(writeRange) is ITfRange range))
            {
                TsfDebugLog.Write("EditSession", "writeRange QI ITfRange 失败");
                return HResult.E_NOINTERFACE;
            }

            int hrSet = range.SetText(ec, 0, pchText, _text.Length);
            TsfDebugLog.Write("EditSession", $"SetText HRESULT=0x{hrSet:X8} cch={_text.Length}");

            var backBuf = Marshal.AllocHGlobal(512 * 2);
            try
            {
                int hrGet = range.GetText(ec, 0, backBuf, 511, out uint pcch);
                string back = hrGet == HResult.S_OK
                    ? Marshal.PtrToStringUni(backBuf, (int)pcch) ?? string.Empty
                    : $"0x{hrGet:X8}";
                TsfDebugLog.Write("EditSession", $"readback hr=0x{hrGet:X8} len={pcch} text='{back}'");
            }
            finally { Marshal.FreeHGlobal(backBuf); }

            if (hrSet == HResult.S_OK)
            {
                PlaceCaretAtEnd(ec, writeRange);
            }

            if (_mode == SessionMode.Commit && hrSet == HResult.S_OK)
            {
                EndCurrentComposition(ec);
                TsfDebugLog.Write("EditSession", "final commit: composition 已结束");
            }

            return hrSet;
        }
        catch (Exception ex)
        {
            TsfDebugLog.Write("EditSession", $"EXCEPTION: {ex.GetType().FullName}: {ex.Message}{Environment.NewLine}{ex.StackTrace}");
            return HResult.E_FAIL;
        }
        finally
        {
            if (pchText != IntPtr.Zero) Marshal.FreeHGlobal(pchText);
            if (compRange != IntPtr.Zero && compRange != pRange) Marshal.Release(compRange);
            if (pRange != IntPtr.Zero) Marshal.Release(pRange);
        }
    }

    private void StartComposition(uint ec, IntPtr pRange)
    {
        TsfDebugLog.Write("EditSession", "StartComposition: 进入");

        // 先用 ITfInsertAtSelection 的 QUERYONLY 在落点处取合法插入范围。
        IntPtr pRangeComp = IntPtr.Zero;
        IntPtr pInsert = IntPtr.Zero;
        IntPtr pUnkInsert = Marshal.GetIUnknownForObject(_context);
        try
        {
            var insertIid = TsfGuids.IID_ITfInsertAtSelection;
            int hrQiInsert = Marshal.QueryInterface(pUnkInsert, ref insertIid, out pInsert);
            TsfDebugLog.Write("EditSession", $"QI ITfInsertAtSelection HRESULT=0x{hrQiInsert:X8} ptr={pInsert}");
            if (hrQiInsert == HResult.S_OK && pInsert != IntPtr.Zero)
            {
                var insert = (ITfInsertAtSelection)Marshal.GetObjectForIUnknown(pInsert);
                int hrIns = insert.InsertTextAtSelection(ec, TsfGuids.TF_IAS_QUERYONLY, IntPtr.Zero, 0, out pRangeComp);
                TsfDebugLog.Write("EditSession", $"InsertTextAtSelection(QUERYONLY) HRESULT=0x{hrIns:X8} range={pRangeComp}");
                if (hrIns != HResult.S_OK || pRangeComp == IntPtr.Zero)
                {
                    pRangeComp = IntPtr.Zero;
                }
            }
        }
        finally
        {
            Marshal.Release(pUnkInsert);
            if (pInsert != IntPtr.Zero) Marshal.Release(pInsert);
        }

        if (pRangeComp == IntPtr.Zero) pRangeComp = pRange;

        var ccIid = TsfGuids.IID_ITfContextComposition;
        IntPtr pUnk = Marshal.GetIUnknownForObject(_context);
        IntPtr pCc = IntPtr.Zero;
        int hrQ;
        try
        {
            hrQ = Marshal.QueryInterface(pUnk, ref ccIid, out pCc);
        }
        finally
        {
            Marshal.Release(pUnk);
        }
        if (hrQ != HResult.S_OK || pCc == IntPtr.Zero)
        {
            TsfDebugLog.Write("EditSession", $"QueryInterface ITfContextComposition HRESULT=0x{hrQ:X8}");
            if (pRangeComp != IntPtr.Zero && pRangeComp != pRange) Marshal.Release(pRangeComp);
            return;
        }

        try
        {
            var cc = (ITfContextComposition)Marshal.GetObjectForIUnknown(pCc);
            IntPtr sink = _owner.CompositionSinkPtr;
            TsfDebugLog.Write("EditSession", $"StartComposition args ec={ec} range={pRangeComp} sink={sink}");
            int hrStart = cc.StartComposition(ec, pRangeComp, sink, out var pComposition);
            TsfDebugLog.Write("EditSession", $"StartComposition(range) HRESULT=0x{hrStart:X8} composition={pComposition}");

            if (hrStart == HResult.S_OK && pComposition != IntPtr.Zero)
            {
                _owner.SetComposition(pComposition);
                // 建区间会话内把光标折叠到插入范围末尾（对齐 Weasel）。
                PlaceCaretAtEnd(ec, pRangeComp);
            }
        }
        finally
        {
            Marshal.Release(pCc);
            if (pRangeComp != IntPtr.Zero && pRangeComp != pRange) Marshal.Release(pRangeComp);
        }
    }

    private void EndCurrentComposition(uint ec)
    {
        if (!_owner.TryGetComposition(out var pComposition)) return;

        try
        {
            var composition = (ITfComposition)Marshal.GetObjectForIUnknown(pComposition);
            int hrEnd = composition.EndComposition(ec);
            TsfDebugLog.Write("EditSession", $"EndComposition HRESULT=0x{hrEnd:X8}");
        }
        finally
        {
            _owner.ClearComposition();
        }
    }

    // TF_ANCHOR_END / TF_AE_NONE 常量（msctf.h）。
    private const int TfAnchorEnd = 1;
    private const int TfActiveSelNone = 0;

    /// <summary>把光标折叠到范围末尾，并通过 SetSelection 写回上下文。</summary>
    private void PlaceCaretAtEnd(uint ec, IntPtr pRange)
    {
        if (!(Marshal.GetObjectForIUnknown(pRange) is ITfRange range))
        {
            TsfDebugLog.Write("EditSession", "PlaceCaretAtEnd: 范围 QI ITfRange 失败");
            return;
        }

        int hrCol = range.Collapse(ec, TfAnchorEnd);
        TsfDebugLog.Write("EditSession", $"Collapse(end) HRESULT=0x{hrCol:X8}");

        var selection = new TfSelection
        {
            Range = pRange,
            ActiveSelEnd = TfActiveSelNone,
            InterimChar = 0
        };
        IntPtr pSel = Marshal.AllocHGlobal(Marshal.SizeOf<TfSelection>());
        try
        {
            Marshal.StructureToPtr(selection, pSel, false);
            int hrSel = _context.SetSelection(ec, 1, pSel);
            TsfDebugLog.Write("EditSession", $"SetSelection HRESULT=0x{hrSel:X8}");
        }
        finally
        {
            Marshal.FreeHGlobal(pSel);
        }
    }
}
