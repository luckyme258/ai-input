using System.Runtime.InteropServices;

namespace AiIme.Tsf.Com;

/// <summary>
/// 按键事件接收器。文本服务实现它并通过 ITfSource::AdviseSink 挂到线程管理器，
/// 以探测/消费拼音按键。pic 为当前 ITfContext 指针，本阶段先以 IntPtr 透传。
///
/// 注意：本接口由我们实现并暴露给原生 TSF 回调，方法返回 HRESULT，必须标
/// <see cref="PreserveSigAttribute"/>，否则运行时会把 out pfEaten 误当返回值推断签名，
/// 造成 vtable 错位、宿主进程访问冲突崩溃。
/// </summary>
[ComVisible(true)]
[ComImport]
[Guid("aa80e7f5-2021-11d2-93e0-0060b067b86e")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface ITfKeyEventSink
{
    [PreserveSig]
    int OnSetFocus(int fforeground);

    [PreserveSig]
    int OnTestKeyDown(IntPtr pic, IntPtr wparam, IntPtr lparam, out int pfEaten);

    [PreserveSig]
    int OnTestKeyUp(IntPtr pic, IntPtr wparam, IntPtr lparam, out int pfEaten);

    [PreserveSig]
    int OnKeyDown(IntPtr pic, IntPtr wparam, IntPtr lparam, out int pfEaten);

    [PreserveSig]
    int OnKeyUp(IntPtr pic, IntPtr wparam, IntPtr lparam, out int pfEaten);

    [PreserveSig]
    int OnPreservedKey(IntPtr pic, ref Guid rguid, out int pfEaten);
}
