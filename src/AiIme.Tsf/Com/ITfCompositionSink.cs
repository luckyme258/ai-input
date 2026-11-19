using System.Runtime.InteropServices;

namespace AiIme.Tsf.Com;

/// <summary>
/// composition 终止回调接口（由文本服务实现，StartComposition 时把自身指针传给系统）。
/// 仅一个回调：composition 被外部终止时通知文本服务清理本地状态。
/// </summary>
[ComVisible(true)]
[ComImport]
[Guid("a781718c-579a-4b15-a280-32b8577acc5e")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface ITfCompositionSink
{
    /// <summary>composition 已被终止（ecWrite 为写会话 cookie，pComposition 为被终止的区间）。</summary>
    [PreserveSig]
    int OnCompositionTerminated(uint ecWrite, IntPtr pComposition);
}
