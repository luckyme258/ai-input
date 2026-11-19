using System.Runtime.InteropServices;

namespace AiIme.Tsf.Com;

/// <summary>
/// 文本服务生命周期接口（我们的 COM 类实现它）。
/// TSF 通过 CoCreateInstance 创建实例后调用 <see cref="Activate"/>，会话结束前调用 <see cref="Deactivate"/>。
///
/// 注意：本接口由我们实现并暴露给原生 TSF 回调，方法返回 HRESULT，必须标
/// <see cref="PreserveSigAttribute"/>，否则运行时会按"最后一个 out 参数作为返回值"
/// 推断本机签名，造成 vtable 签名错位、宿主进程访问冲突崩溃。
/// </summary>
[ComImport]
[Guid("aa80e7f7-2021-11d2-93e0-0060b067b86e")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface ITfTextInputProcessor
{
    /// <summary>激活文本服务。ptim 为线程管理器，tid 为客户端标识。</summary>
    [PreserveSig]
    int Activate(IntPtr ptim, uint tid);

    /// <summary>停用文本服务，释放对 ptim 等的所有引用。</summary>
    [PreserveSig]
    int Deactivate();
}
