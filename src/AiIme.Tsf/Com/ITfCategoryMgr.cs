using System.Runtime.InteropServices;

namespace AiIme.Tsf.Com;

/// <summary>
/// TSF 类别管理器接口（由 msctf 实现）。本项目只用到类别注册/注销：
/// 把本输入法 CLSID 挂到键盘类别 <see cref="TsfGuids.GUID_TFCAT_TIP_KEYBOARD"/>，
/// TSF 才会把它作为键盘文本服务列入输入法列表。
/// vtable 顺序取自 windows-rs msctf 元数据；后续方法在需要时按顺序补齐，不可在中间插方法。
/// </summary>
[ComImport]
[Guid("c3acefb5-f69d-4905-938f-fcadcf4be830")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface ITfCategoryMgr
{
    /// <summary>把 <paramref name="rguid"/>（通常即本服务 CLSID）登记到类别 <paramref name="rcatid"/>。</summary>
    int RegisterCategory(IntPtr rclsid, IntPtr rcatid, IntPtr rguid);

    /// <summary>移除已登记的类别项。</summary>
    int UnregisterCategory(IntPtr rclsid, IntPtr rcatid, IntPtr rguid);
}
