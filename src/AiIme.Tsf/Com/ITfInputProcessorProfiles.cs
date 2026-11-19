using System.Runtime.InteropServices;

namespace AiIme.Tsf.Com;

/// <summary>
/// TSF 输入处理器配置接口（由 TSF 管理器实现）。注册/卸载文本服务与语言 profile 时使用。
/// vtable 顺序取自 windows-rs msctf 元数据；本项目只用到前 4 个方法，
/// 后续方法（EnumInputProcessorInfo 等）在需要时按顺序补齐，不可在中间插方法。
/// </summary>
[ComImport]
[Guid("1f02b6c5-7842-4ee6-8a0b-9a24183a95ca")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface ITfInputProcessorProfiles
{
    /// <summary>向 TSF 注册文本服务 CLSID。rclsid 为指向 GUID 的原生指针。</summary>
    int Register(IntPtr rclsid);

    /// <summary>注销文本服务及其全部语言 profile。rclsid 为指向 GUID 的原生指针。</summary>
    int Unregister(IntPtr rclsid);

    /// <summary>为指定语言添加语言 profile。GUID/字符串均以原生指针传入。</summary>
    int AddLanguageProfile(
        IntPtr rclsid,
        ushort langid,
        IntPtr guidProfile,
        IntPtr pchDesc,
        int cchDesc,
        IntPtr pchIconFile,
        int cchFile,
        uint uIconIndex);

    /// <summary>移除指定语言 profile。</summary>
    int RemoveLanguageProfile(IntPtr rclsid, ushort langid, IntPtr guidProfile);
}
