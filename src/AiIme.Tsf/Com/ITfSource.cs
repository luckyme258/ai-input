using System.Runtime.InteropServices;

namespace AiIme.Tsf.Com;

/// <summary>
/// 事件源接口：用于 AdviseSink / UnadviseSink 挂载事件接收器（如 ITfKeyEventSink）。
/// </summary>
[ComImport]
[Guid("4ea48a35-60ae-446f-8fd6-e6a8d82459f7")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface ITfSource
{
    [PreserveSig]
    int AdviseSink(ref Guid riid, IntPtr punk, out uint pdwCookie);

    [PreserveSig]
    int UnadviseSink(uint dwCookie);
}
