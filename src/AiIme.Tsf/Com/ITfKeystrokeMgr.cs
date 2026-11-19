using System.Runtime.InteropServices;

namespace AiIme.Tsf.Com;

/// <summary>
/// 按键管理器（系统实现）。文本服务通过它挂接 <see cref="ITfKeyEventSink"/>，
/// 而不是 ITfSource::AdviseSink。vtable 顺序对照 windows-rs msctf 元数据。
///
/// 注意：方法返回 HRESULT，必须标 <see cref="PreserveSigAttribute"/>。
/// </summary>
[ComVisible(true)]
[ComImport]
[Guid("aa80e7f0-2021-11d2-93e0-0060b067b86e")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface ITfKeystrokeMgr
{
    /// <summary>挂接按键事件接收器。tid 为 Activate 收到的 clientId；fforeground 指示是否前台接收。</summary>
    [PreserveSig]
    int AdviseKeyEventSink(uint tid, IntPtr psink, int fforeground);

    /// <summary>按 clientId 卸载按键事件接收器。</summary>
    [PreserveSig]
    int UnadviseKeyEventSink(uint tid);

    // 后续槽位（GetForeground / TestKeyDown / TestKeyUp / KeyDown / KeyUp /
    // GetPreservedKey / IsPreservedKey / PreserveKey / UnpreserveKey / ...）暂未声明，
    // 需要时按元数据顺序在对应位置补入，不得打乱顺序。
}

/// <summary>
/// 手写 vtable 委托：直接从原生对象 vtable 槽位取函数指针调用，
/// 绕过 RCW 对消费端接口的方法封送（后者在 comhost ALC 下触发 CLR 内部错误 0x80131506）。
/// 仅取本服务用到的前两个槽位。
/// </summary>
internal static class KeystrokeMgrNative
{
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    public delegate int AdviseKeyEventSinkDelegate(IntPtr self, uint tid, IntPtr psink, int fforeground);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    public delegate int UnadviseKeyEventSinkDelegate(IntPtr self, uint tid);

    /// <summary>从原生对象指针取 vtable 第 1 槽（AdviseKeyEventSink）。</summary>
    public static AdviseKeyEventSinkDelegate GetAdvise(IntPtr mgrPtr)
    {
        IntPtr vtbl = Marshal.ReadIntPtr(mgrPtr);
        IntPtr fn = Marshal.ReadIntPtr(vtbl, IntPtr.Size * 3); // IUnknown 占 3 槽，第 1 个自有方法
        return Marshal.GetDelegateForFunctionPointer<AdviseKeyEventSinkDelegate>(fn);
    }

    /// <summary>从原生对象指针取 vtable 第 2 槽（UnadviseKeyEventSink）。</summary>
    public static UnadviseKeyEventSinkDelegate GetUnadvise(IntPtr mgrPtr)
    {
        IntPtr vtbl = Marshal.ReadIntPtr(mgrPtr);
        IntPtr fn = Marshal.ReadIntPtr(vtbl, IntPtr.Size * 4);
        return Marshal.GetDelegateForFunctionPointer<UnadviseKeyEventSinkDelegate>(fn);
    }
}

