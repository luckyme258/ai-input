using System.Runtime.InteropServices;

namespace AiIme.Tsf.Com;

/// <summary>
/// TSF 文本范围（系统实现）。引用文档中的一段文本，可读取/写入/折叠。
/// vtable 顺序取自 windows-rs msctf 元数据（共 27 个槽位）。
///
/// 最小上屏链路只用到 <see cref="SetText"/>；其余槽位按序以 IntPtr 占位，
/// 后续需要哪个方法，再把对应占位替换为真实签名，位置不得前移或后移。
/// </summary>
[ComImport]
[Guid("aa80e7ff-2021-11d2-93e0-0060b067b86e")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface ITfRange
{
    // 1. GetText(ec, dwflags, pchtext, cchmax, pcch) —— 占位
    int Placeholder_GetText(uint ec, uint dwflags, IntPtr pchText, uint cchMax, IntPtr pcch);

    /// <summary>在范围内写入文本（覆盖选区）；pchText 为宽字符指针，cch 为字符数。</summary>
    int SetText(uint ec, uint dwflags, IntPtr pchText, int cch);

    // 3. GetFormattedText(ec, ppDataObject)
    int Placeholder_GetFormattedText(uint ec, out IntPtr ppDataObject);

    // 4. GetEmbedded(ec, rguidService, riid, ppunk)
    int Placeholder_GetEmbedded(uint ec, IntPtr rguidService, IntPtr riid, out IntPtr ppunk);

    // 5. InsertEmbedded(ec, dwflags, pDataObject)
    int Placeholder_InsertEmbedded(uint ec, uint dwflags, IntPtr pDataObject);

    // 6. ShiftStart(ec, cchReq, pcch, pHaltCond)
    int Placeholder_ShiftStart(uint ec, int cchReq, IntPtr pcch, IntPtr pHaltCond);

    // 7. ShiftEnd(ec, cchReq, pcch, pHaltCond)
    int Placeholder_ShiftEnd(uint ec, int cchReq, IntPtr pcch, IntPtr pHaltCond);

    // 8. ShiftStartToRange(ec, pRange, aPos)
    int Placeholder_ShiftStartToRange(uint ec, IntPtr pRange, int aPos);

    // 9. ShiftEndToRange(ec, pRange, aPos)
    int Placeholder_ShiftEndToRange(uint ec, IntPtr pRange, int aPos);

    // 10. ShiftStartRegion(ec, dir, pfNoRegion)
    int Placeholder_ShiftStartRegion(uint ec, int dir, IntPtr pfNoRegion);

    // 11. ShiftEndRegion(ec, dir, pfNoRegion)
    int Placeholder_ShiftEndRegion(uint ec, int dir, IntPtr pfNoRegion);

    // 12. IsEmpty(ec, pfEmpty)
    int Placeholder_IsEmpty(uint ec, IntPtr pfEmpty);

    // 13. Collapse(ec, aPos)
    int Placeholder_Collapse(uint ec, int aPos);

    // 14. IsEqualStart(ec, pWith, aPos, pfEqual)
    int Placeholder_IsEqualStart(uint ec, IntPtr pWith, int aPos, IntPtr pfEqual);

    // 15. IsEqualEnd(ec, pWith, aPos, pfEqual)
    int Placeholder_IsEqualEnd(uint ec, IntPtr pWith, int aPos, IntPtr pfEqual);

    // 16. CompareStart(ec, pWith, aPos, plResult)
    int Placeholder_CompareStart(uint ec, IntPtr pWith, int aPos, IntPtr plResult);

    // 17. CompareEnd(ec, pWith, aPos, plResult)
    int Placeholder_CompareEnd(uint ec, IntPtr pWith, int aPos, IntPtr plResult);

    // 18. AdjustForInsert(ec, cchInsert, pfInsertOk)
    int Placeholder_AdjustForInsert(uint ec, uint cchInsert, IntPtr pfInsertOk);

    // 19. GetGravity(pgStart, pgEnd)
    int Placeholder_GetGravity(IntPtr pgStart, IntPtr pgEnd);

    // 20. SetGravity(ec, gStart, gEnd)
    int Placeholder_SetGravity(uint ec, int gStart, int gEnd);

    // 21. Clone(ppClone)
    int Placeholder_Clone(out IntPtr ppClone);

    // 22. GetContext(ppContext)
    int Placeholder_GetContext(out IntPtr ppContext);
}
