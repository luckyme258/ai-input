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

    public EditSession(ITfContext context, string text, CompositionComposer owner)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _text = text ?? string.Empty;
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
    }

    public int DoEditSession(uint ec)
    {
        TsfDebugLog.Write("EditSession", $"DoEditSession ec={ec} text='{_text}' composing={_owner.HasComposition}");

        IntPtr pRange = IntPtr.Zero;
        IntPtr pchText = IntPtr.Zero;
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

            // 3. 首次输入：在选区范围上启动 composition 区间。
            if (!_owner.HasComposition)
            {
                StartComposition(ec, pRange);
            }

            // 4. 覆盖写入当前拼音串。
            pchText = Marshal.StringToHGlobalUni(_text);
            // dwflags=0：默认行为；cch 为字符数（不含终止符）。
            int hrSet = Marshal.GetObjectForIUnknown(pRange) is ITfRange range
                ? range.SetText(ec, 0, pchText, _text.Length)
                : HResult.E_NOINTERFACE;
            TsfDebugLog.Write("EditSession", $"SetText HRESULT=0x{hrSet:X8} cch={_text.Length}");
            return hrSet;
        }
        finally
        {
            if (pchText != IntPtr.Zero) Marshal.FreeHGlobal(pchText);
            if (pRange != IntPtr.Zero) Marshal.Release(pRange);
        }
    }

    private void StartComposition(uint ec, IntPtr pRange)
    {
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
            int hrStart = cc.StartComposition(ec, pRange, IntPtr.Zero, out var pComposition);
            TsfDebugLog.Write("EditSession", $"StartComposition HRESULT=0x{hrStart:X8} composition={pComposition}");
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
}
