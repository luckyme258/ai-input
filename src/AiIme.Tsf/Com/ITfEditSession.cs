using System.Runtime.InteropServices;

namespace AiIme.Tsf.Com;

/// <summary>
/// 编辑会话回调接口。文本服务实现它，通过 ITfContext.RequestEditSession 提交；
/// TSF 在持有写 cookie 的会话内回调 <see cref="DoEditSession"/>，期间才可读写文本。
///
/// 注意：本接口由我们实现并暴露给原生 TSF 回调，方法返回 HRESULT，必须标
/// <see cref="PreserveSigAttribute"/>，避免运行时推断出错误的本机签名。
/// </summary>
[ComVisible(true)]
[ComImport]
[Guid("aa80e803-2021-11d2-93e0-0060b067b86e")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface ITfEditSession
{
    /// <summary>在编辑会话内执行；ec 为 TSF 发放的读写 cookie。</summary>
    [PreserveSig]
    int DoEditSession(uint ec);
}
