using System.Runtime.InteropServices;

namespace AiIme.Tsf.Com;

/// <summary>
/// TSF (msctf) 关键 GUID。IID 取自微软 windows-rs 元数据（对应 msctf.h），
/// CLSID_TF_ThreadMgr / CLSID_TF_InputProcessorProfiles 为系统预定义 COM 类。
/// </summary>
internal static class TsfGuids
{
    // IID_ITfTextInputProcessor
    public static readonly Guid IID_ITfTextInputProcessor = new("aa80e7f7-2021-11d2-93e0-0060b067b86e");

    // IID_ITfTextInputProcessorEx
    public static readonly Guid IID_ITfTextInputProcessorEx = new("6e4e2102-f9cd-433d-b496-303ce03a6507");

    // IID_ITfThreadMgr
    public static readonly Guid IID_ITfThreadMgr = new("aa80e801-2021-11d2-93e0-0060b067b86e");

    // IID_ITfKeystrokeMgr（按键管理器，挂接 ITfKeyEventSink 的正确入口）
    public static readonly Guid IID_ITfKeystrokeMgr = new("aa80e7f0-2021-11d2-93e0-0060b067b86e");

    // IID_ITfKeyEventSink
    public static readonly Guid IID_ITfKeyEventSink = new("aa80e7f5-2021-11d2-93e0-0060b067b86e");

    // IID_ITfSource
    public static readonly Guid IID_ITfSource = new("4ea48a35-60ae-446f-8fd6-e6a8d82459f7");

    // CLSID_TF_ThreadMgr（本机注册表 TF_ThreadMgr 实测核对）
    public static readonly Guid CLSID_TF_ThreadMgr = new("529a9e6b-6587-4f23-ab9e-9c7d683e3c50");

    // CLSID_TF_InputProcessorProfiles（本机注册表 TF_InputProcessorProfiles 实测核对）
    public static readonly Guid CLSID_TF_InputProcessorProfiles = new("33c53a50-f456-4884-b049-85fd643ecfed");

    // 本输入法文本服务的 CLSID（本项目自分配，安装注册时使用；需在注册表与 ComVisible 类上保持一致）。
    public static readonly Guid CLSID_AiImeTextService = new("8f3c2a10-7d44-4c9b-9e2a-1b6d5a7c8f90");

    // 本输入法语言 profile GUID（与 CLSID 区分；一个文本服务可挂多个 profile）。
    public static readonly Guid GUID_AiImeProfile = new("9a1c7d42-3e6b-4f0a-8c5d-2b7e1f0a9c34");

    // 简体中文 LANGID：0x0804（主语言 0x04 + 子语言 0x08）。
    public const ushort LangIdZhCn = 0x0804;

    // RequestEditSession 标志（TF_ES_*）：同步 + 读写。
    public const uint TF_ES_SYNC = 1;
    public const uint TF_ES_READ = 2;
    public const uint TF_ES_READWRITE = 6;

    // IID_ITfInputProcessorProfiles
    public static readonly Guid IID_ITfInputProcessorProfiles = new("1f02b6c5-7842-4ee6-8a0b-9a24183a95ca");

    // IID_ITfCategoryMgr（windows-rs msctf 元数据核对）
    public static readonly Guid IID_ITfCategoryMgr = new("c3acefb5-f69d-4905-938f-fcadcf4be830");

    // IID_ITfContextComposition（windows-rs msctf 元数据核对）
    public static readonly Guid IID_ITfContextComposition = new("d40c8aae-ac92-4fc7-9a11-0ee0e23aa39b");

    // IID_ITfComposition（windows-rs msctf 元数据核对）
    public static readonly Guid IID_ITfComposition = new("20168d64-5a8f-4a5a-b7bd-cfa29f4d0fd9");

    // IID_ITfInsertAtSelection（msctf.tlb / Learn 元数据核对）
    public static readonly Guid IID_ITfInsertAtSelection = new("55ce16ba-3014-41c1-9ceb-fade1446ac6c");

    // IID_ITfCompositionSink（msctf.tlb / Learn 元数据核对）
    public static readonly Guid IID_ITfCompositionSink = new("a781718c-579a-4b15-a280-32b8577acc5e");

    // InsertTextAtSelection 标志：NOQUERY 退出时范围置空；QUERYONLY 不真正写入，仅返回落点范围。
    public const uint TF_IAS_NOQUERY = 1;
    public const uint TF_IAS_QUERYONLY = 2;

    // GUID_TFCAT_TIP_KEYBOARD：键盘类型文本服务类别（C# TSF 源码 + 本机注册表交叉确认）。
    public static readonly Guid GUID_TFCAT_TIP_KEYBOARD = new("34745c63-b2f0-4784-8b67-5e12c8701a31");
}

/// <summary>TSF 常见 HRESULT。</summary>
internal static class HResult
{
    public const int S_OK = 0;
    public const int S_FALSE = 1;
    public const int E_NOINTERFACE = unchecked((int)0x80004002);
    public const int E_POINTER = unchecked((int)0x80004003);
    public const int E_INVALIDARG = unchecked((int)0x80070057);
    public const int E_FAIL = unchecked((int)0x80004005);
}
