using System.Runtime.InteropServices;

namespace AiIme.Tsf.Com;

/// <summary>
/// 在选区/落点处插入文本或嵌入对象的上下文服务（由系统实现，从 ITfContext QI 获取）。
/// vtable 顺序（msctf.h / windows-rs）：InsertTextAtSelection → InsertEmbeddedAtSelection。
/// </summary>
[ComImport]
[Guid("55ce16ba-3014-41c1-9ceb-fade1446ac6c")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface ITfInsertAtSelection
{
    /// <summary>
    /// 在当前选区处插入文本；dwFlags 为 TF_IAS_QUERYONLY 时不写入，仅返回落点范围。
    /// </summary>
    [PreserveSig]
    int InsertTextAtSelection(uint ec, uint dwFlags, IntPtr pchText, int cch, out IntPtr ppRange);

    // 槽 2：InsertEmbeddedAtSelection(ec, dwFlags, pDataObject, ppRange) —— 占位，当前不调用。
    int Placeholder_InsertEmbeddedAtSelection(uint ec, uint dwFlags, IntPtr pDataObject, out IntPtr ppRange);
}
