using System.Runtime.InteropServices;
using AiIme.Tsf.Com;

namespace AiIme.Tsf.TextService;

/// <summary>
/// 只读编辑会话：在会话内取当前选区范围，并经 <see cref="ITfContextView.GetTextExt"/>
/// 读取该选区在屏幕坐标下的外接矩形（光标/拼音串位置），供候选窗定位。
/// </summary>
[ClassInterface(ClassInterfaceType.None)]
internal sealed class CaretRectSession : ITfEditSession
{
    private readonly ITfContext _context;
    private readonly ITfContextView _view;

    /// <summary>取到的屏幕矩形；<see cref="Succeeded"/> 为真时有效。</summary>
    public TfRect Rect;

    /// <summary>GetTextExt 是否成功返回有效矩形。</summary>
    public bool Succeeded;

    public CaretRectSession(ITfContext context, ITfContextView view)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _view = view ?? throw new ArgumentNullException(nameof(view));
    }

    public int DoEditSession(uint ec)
    {
        IntPtr pRange = IntPtr.Zero;
        try
        {
            int hrSel = _context.GetSelection(ec, 0, 1, out var selection, out _);
            if (hrSel != HResult.S_OK) return hrSel;

            pRange = selection.Range;
            if (pRange == IntPtr.Zero) return HResult.S_OK;

            var rect = new TfRect();
            int hrExt = _view.GetTextExt(ec, pRange, ref rect, out _);
            if (hrExt == HResult.S_OK)
            {
                Rect = rect;
                // 折叠光标（选区为空）矩形高度为 0，但仍有有效的 X/Y 落点，不能判空。
                bool hasPoint = rect.Right > rect.Left || rect.Bottom > rect.Top;
                Succeeded = hasPoint && !(rect.Left == 0 && rect.Top == 0 && rect.Right == 0 && rect.Bottom == 0);
            }
            return hrExt;
        }
        finally
        {
            if (pRange != IntPtr.Zero) Marshal.Release(pRange);
        }
    }
}
