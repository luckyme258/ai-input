using System.Runtime.InteropServices;
using AiIme.Tsf.Com;

namespace AiIme.Tsf.TextService;

/// <summary>
/// 一次性编辑会话：在 TSF 发放写 cookie 的会话内，取选区范围、维护 composition 区间、
/// 把指定文本覆盖写入。由 <see cref="CompositionComposer"/> 通过 ITfContext.RequestEditSession 提交。
///
/// 生命周期：
/// - 首次有文本时，经 ITfContextComposition.StartComposition 在选区范围上创建区间；
/// - 文本清空（退格删空 / Esc）时，EndComposition 结束区间；
/// - 当前 composition 引用由 <see cref="CompositionComposer"/> 跨会话持有。
/// </summary>
[ClassInterface(ClassInterfaceType.None)]
public sealed class EditSession : ITfEditSession
{
    private readonly ITfContext _context;
    private readonly string _text;
    private readonly CompositionComposer _owner;
    private readonly bool _finalCommit;

    public EditSession(ITfContext context, string text, CompositionComposer owner, bool finalCommit = false)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _text = text ?? string.Empty;
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        _finalCommit = finalCommit;
    }

    public int DoEditSession(uint ec)
    {
        TsfDebugLog.Write("EditSession", $"DoEditSession ec={ec} text='{_text}' composing={_owner.HasComposition}");

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

            // 诊断：当前会话是否持有写锁。
            int hrW = _context.InWriteSession(_owner.ClientId, out int writeSession);
            TsfDebugLog.Write("EditSession", $"InWriteSession hr=0x{hrW:X8} write={writeSession}");

            if (pRange == IntPtr.Zero)
            {
                // 无选区（如空文档无落点）：无可写范围，本次不写。
                return HResult.S_OK;
            }

            // 2. 文本为空：结束已有 composition 区间，清空选区。
            if (_text.Length == 0)
            {
                EndCurrentComposition(ec);
                TsfDebugLog.Write("EditSession", "文本为空，结束 composition");
                return HResult.S_OK;
            }

            pchText = Marshal.StringToHGlobalUni(_text);

            // 3. 首次输入：在选区范围上启动 composition 区间。
            if (!_owner.HasComposition)
            {
                StartComposition(ec, pRange);
            }

            // 4. 取 composition 自身覆盖范围（会随插入文本自动扩展）；取不到再回退选区范围。
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

            // 5. 覆盖写入当前拼音串。
            // dwflags=0：默认行为；cch 为字符数（不含终止符）。
            if (Marshal.GetObjectForIUnknown(writeRange) is ITfRange range)
            {
                int hrSet = range.SetText(ec, 0, pchText, _text.Length);
                TsfDebugLog.Write("EditSession", $"SetText HRESULT=0x{hrSet:X8} cch={_text.Length}");

                // 回读范围内容，确认文本是否真正写入文档。
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

                // 最终上屏（选词/回车）：写入成功后结束 composition 区间，把文本固化为普通正文。
                if (hrSet == HResult.S_OK)
                {
                    PlaceCaretAtEnd(ec, writeRange);
                }

                if (hrSet == HResult.S_OK && _finalCommit)
                {
                    EndCurrentComposition(ec);
                    TsfDebugLog.Write("EditSession", "final commit: composition 已结束");
                }
                return hrSet;
            }

            TsfDebugLog.Write("EditSession", "writeRange QI ITfRange 失败");
            return HResult.E_NOINTERFACE;
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

        // 参照标准 TSF 流程（Weasel SampleIME）：先用 ITfInsertAtSelection 的
        // TF_IAS_QUERYONLY 在落点处取一个合法插入范围，而不是直接用 GetSelection 的选区范围。
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

        // 取不到插入范围时回退到选区范围。
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
            }
        }
        finally
        {
            Marshal.Release(pCc);
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

    /// <summary>把光标折叠到范围末尾，并通过 SetSelection 写回上下文，触发宿主重绘与后续退格定位。</summary>
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
