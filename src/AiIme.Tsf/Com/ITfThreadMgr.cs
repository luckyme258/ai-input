using System.Runtime.InteropServices;

namespace AiIme.Tsf.Com;

/// <summary>
/// TSF 线程管理器（系统实现，文本服务在 Activate 时收到其指针）。
/// vtable 顺序对照 msctf.h / windows-rs 元数据；本阶段只声明需要的方法，
/// 后续补文档管理器、焦点、函数提供者等方法时按顺序补齐。
/// </summary>
[ComImport]
[Guid("aa80e801-2021-11d2-93e0-0060b067b86e")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface ITfThreadMgr
{
    // IUnknown 方法由 ComImport 运行时处理，下方按 TSF vtable 顺序排列。

    /// <summary>激活线程管理器，返回客户端标识 TfClientId。</summary>
    int Activate(out uint ptid);

    /// <summary>停用线程管理器。</summary>
    int Deactivate();

    // 占位：CreateDocumentMgr / EnumDocumentMgrs / GetFocus / SetFocus /
    // AssociateFocus / IsThreadFocus / GetFunctionProvider / EnumFunctionProviders /
    // GetGlobalCompartment 暂未声明，需要时在对应位置按顺序补入，不得打乱顺序。
}
