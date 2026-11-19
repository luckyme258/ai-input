using System.Runtime.InteropServices;
using AiIme.Tsf.Com;
using AiIme.Tsf.TextService;
using Microsoft.Win32;

namespace AiIme.Tsf.Registration;

/// <summary>
/// TSF 文本服务注册/卸载。分两部分：
///
/// 1. COM 类注册（直写注册表）：CLSID → 原生 comhost.dll（.NET Core/5+ COM hosting），
///    运行时引导由 comhost 同目录的 .runtimeconfig.json / .deps.json 完成，
///    不使用 .NET Framework 时代的 Assembly/Class/CodeBase 元数据。
/// 2. TSF profile 注册（COM 调用）：CoCreateInstance(CLSID_TF_InputProcessorProfiles)，
///    调 <see cref="ITfInputProcessorProfiles.Register"/> 与 AddLanguageProfile。
///
/// 注意：写 HKLM 与注册 TSF profile 都需要管理员权限；卸载必须对称清理。
/// </summary>
public sealed class TsfRegistrar
{
    private readonly Guid _clsid = TsfGuids.CLSID_AiImeTextService;
    private readonly Guid _profile = TsfGuids.GUID_AiImeProfile;
    private readonly string _comhostPath;
    private readonly string _description;

    /// <param name="comhostPath">AiIme.Tsf.comhost.dll 的绝对路径（与 dll/runtimeconfig 同目录）。</param>
    /// <param name="description">语言 profile 显示名。</param>
    public TsfRegistrar(string comhostPath, string description = "AiIme 拼音")
    {
        _comhostPath = comhostPath ?? throw new ArgumentNullException(nameof(comhostPath));
        _description = description ?? throw new ArgumentNullException(nameof(description));
    }

    /// <summary>注册文本服务（COM 类项 + TSF profile + 类别）。需管理员。</summary>
    public void Register()
    {
        RunOnStaThread(() =>
        {
            RegisterComClass();
            RegisterTsfProfile();
            RegisterCategories();
        });
    }

    /// <summary>卸载文本服务（类别 + TSF profile + COM 类项）。需管理员。</summary>
    public void Unregister()
    {
        RunOnStaThread(() =>
        {
            UnregisterCategories();
            UnregisterTsfProfile();
            UnregisterComClass();
        });
    }

    /// <summary>
    /// CLR 控制台主线程默认 MTA；TSF profiles 对象 ThreadingModel=Apartment，
    /// 在 MTA 下跨套间编组会让引用类型指针被代理转发（存根报空指针、直调踩栈）。
    /// 因此把整段 COM 注册/卸载放到一个显式 STA 线程上执行。
    /// </summary>
    private static void RunOnStaThread(Action action)
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                error = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = false;
        thread.Start();
        thread.Join();
        if (error != null) throw error;
    }

    // ---- 1. COM 类注册表项 ----

    private void RegisterComClass()
    {
        if (!File.Exists(_comhostPath))
            throw new FileNotFoundException("comhost.dll 不存在，无法注册 COM 类。", _comhostPath);

        using var clsidKey = Registry.ClassesRoot.CreateSubKey($@"CLSID\{_clsid:B}");
        clsidKey.SetValue(null, _description);

        using var inproc = clsidKey.CreateSubKey("InprocServer32");
        inproc.SetValue(null, _comhostPath);
        inproc.SetValue("ThreadingModel", "Both");
    }

    private void UnregisterComClass()
    {
        Registry.ClassesRoot.DeleteSubKeyTree($@"CLSID\{_clsid:B}", false);
    }

    // ---- 2. TSF profile（COM 调用）----

    private void RegisterTsfProfile()
    {
        var profiles = CreateProfiles();
        IntPtr pClsid = IntPtr.Zero;
        IntPtr pProfile = IntPtr.Zero;
        IntPtr pchDesc = IntPtr.Zero;
        try
        {
            // GUID 与描述串都用非托管内存固定后传原生指针。
            pClsid = Marshal.AllocHGlobal(Marshal.SizeOf<Guid>());
            Marshal.StructureToPtr(_clsid, pClsid, false);
            pProfile = Marshal.AllocHGlobal(Marshal.SizeOf<Guid>());
            Marshal.StructureToPtr(_profile, pProfile, false);
            pchDesc = Marshal.StringToHGlobalUni(_description);

            int hrReg = profiles.Register(pClsid);
            TsfDebugLog.Write("RegisterTsfProfile", $"Register HRESULT=0x{hrReg:X8}");
            Check(hrReg, "ITfInputProcessorProfiles.Register 失败");

            // cchDesc=-1 表示 pchDesc 为 null 结尾宽串；图标文件可空，传 Zero。
            int hrAdd = profiles.AddLanguageProfile(
                pClsid,
                TsfGuids.LangIdZhCn,
                pProfile,
                pchDesc,
                -1,
                IntPtr.Zero,
                -1,
                0);
            TsfDebugLog.Write("RegisterTsfProfile", $"AddLanguageProfile HRESULT=0x{hrAdd:X8}");
            Check(hrAdd, "ITfInputProcessorProfiles.AddLanguageProfile 失败");

            // AddLanguageProfile 不会写启用标志；本机正常 profile 节点都带 Enable（1 启用 / 0 禁用）。
            // 缺该值时语言栏/语言列表可能不把本输入法列为可用，这里直写 Enable=1。
            EnableProfile();
        }
        finally
        {
            if (pchDesc != IntPtr.Zero) Marshal.FreeHGlobal(pchDesc);
            if (pProfile != IntPtr.Zero) Marshal.FreeHGlobal(pProfile);
            if (pClsid != IntPtr.Zero) Marshal.FreeHGlobal(pClsid);
            Marshal.ReleaseComObject(profiles);
        }
    }

    /// <summary>在 profile 注册表节点写 Enable=1（DWORD）。</summary>
    private void EnableProfile()
    {
        var subKey = $@"SOFTWARE\Microsoft\CTF\TIP\{_clsid:B}\LanguageProfile\0x{TsfGuids.LangIdZhCn:X8}\{_profile:B}";
        using var key = Registry.LocalMachine.CreateSubKey(subKey);
        key.SetValue("Enable", 1, RegistryValueKind.DWord);
        TsfDebugLog.Write("RegisterTsfProfile", $"Enable=1 written to {subKey}");
    }

    private void UnregisterTsfProfile()    {
        var profiles = CreateProfiles();
        IntPtr pClsid = IntPtr.Zero;
        try
        {
            pClsid = Marshal.AllocHGlobal(Marshal.SizeOf<Guid>());
            Marshal.StructureToPtr(_clsid, pClsid, false);
            // Unregister 会移除该 CLSID 的全部语言 profile；先尝试精确移除，再整体注销。
            Check(profiles.Unregister(pClsid), "ITfInputProcessorProfiles.Unregister 失败");
        }
        catch (COMException)
        {
            // 卸载时若 profile 本就不存在，忽略，避免阻断清理。
        }
        finally
        {
            if (pClsid != IntPtr.Zero) Marshal.FreeHGlobal(pClsid);
            Marshal.ReleaseComObject(profiles);
        }
    }

    // ---- 3. TSF 类别注册（键盘文本服务）----

    private void RegisterCategories()
    {
        var catMgr = CreateCategoryMgr();
        IntPtr pClsid = IntPtr.Zero;
        IntPtr pCatid = IntPtr.Zero;
        try
        {
            pClsid = Marshal.AllocHGlobal(Marshal.SizeOf<Guid>());
            Marshal.StructureToPtr(_clsid, pClsid, false);
            pCatid = Marshal.AllocHGlobal(Marshal.SizeOf<Guid>());
            Marshal.StructureToPtr(TsfGuids.GUID_TFCAT_TIP_KEYBOARD, pCatid, false);

            // RegisterCategory(rclsid=本服务, rcatid=键盘类别, rguid=本服务 CLSID)。
            int hr = catMgr.RegisterCategory(pClsid, pCatid, pClsid);
            TsfDebugLog.Write("RegisterCategories", $"RegisterCategory(KEYBOARD) HRESULT=0x{hr:X8}");
            Check(hr, "ITfCategoryMgr.RegisterCategory 失败");
        }
        finally
        {
            if (pCatid != IntPtr.Zero) Marshal.FreeHGlobal(pCatid);
            if (pClsid != IntPtr.Zero) Marshal.FreeHGlobal(pClsid);
            Marshal.ReleaseComObject(catMgr);
        }
    }

    private void UnregisterCategories()
    {
        var catMgr = CreateCategoryMgr();
        IntPtr pClsid = IntPtr.Zero;
        IntPtr pCatid = IntPtr.Zero;
        try
        {
            pClsid = Marshal.AllocHGlobal(Marshal.SizeOf<Guid>());
            Marshal.StructureToPtr(_clsid, pClsid, false);
            pCatid = Marshal.AllocHGlobal(Marshal.SizeOf<Guid>());
            Marshal.StructureToPtr(TsfGuids.GUID_TFCAT_TIP_KEYBOARD, pCatid, false);

            int hr = catMgr.UnregisterCategory(pClsid, pCatid, pClsid);
            TsfDebugLog.Write("UnregisterCategories", $"UnregisterCategory(KEYBOARD) HRESULT=0x{hr:X8}");
            // 卸载时类别可能本就不存在，不阻断清理。
        }
        catch (COMException)
        {
            // 忽略：类别未注册或已被移除。
        }
        finally
        {
            if (pCatid != IntPtr.Zero) Marshal.FreeHGlobal(pCatid);
            if (pClsid != IntPtr.Zero) Marshal.FreeHGlobal(pClsid);
            Marshal.ReleaseComObject(catMgr);
        }
    }

    private static ITfCategoryMgr CreateCategoryMgr()
    {
        // 用 msctf.dll 导出的 TF_CreateCategoryMgr 创建，无需固定 CLSID_TF_CategoryMgr。
        int hr = TF_CreateCategoryMgr(out var ppv);
        TsfDebugLog.Write("CreateCategoryMgr", $"TF_CreateCategoryMgr HRESULT=0x{hr:X8} ppv={ppv}");
        Check(hr, "TF_CreateCategoryMgr 失败");
        return (ITfCategoryMgr)Marshal.GetObjectForIUnknown(ppv);
    }

    private static ITfInputProcessorProfiles CreateProfiles()
    {
        // 调用 COM 前先初始化套间；重复初始化返回 S_FALSE，忽略即可。
        int hrCoInit = CoInitializeEx(IntPtr.Zero, COINIT_APARTMENTTHREADED);
        TsfDebugLog.Write("CreateProfiles", $"CoInitializeEx HRESULT=0x{hrCoInit:X8} apartment={Thread.CurrentThread.GetApartmentState()}");

        var clsid = TsfGuids.CLSID_TF_InputProcessorProfiles;
        var iid = TsfGuids.IID_ITfInputProcessorProfiles;
        int hr = CoCreateInstance(ref clsid, IntPtr.Zero, 1 /* CLSCTX_INPROC_SERVER */, ref iid, out var ppv);
        TsfDebugLog.Write("CreateProfiles", $"CoCreateInstance HRESULT=0x{hr:X8} ppv={ppv}");
        Check(hr, "CoCreateInstance(CLSID_TF_InputProcessorProfiles) 失败");
        return (ITfInputProcessorProfiles)Marshal.GetObjectForIUnknown(ppv);
    }

    private static void Check(int hr, string message)
    {
        if (hr != HResult.S_OK)
            throw new COMException($"{message} (HRESULT=0x{hr:X8})", hr);
    }

    [DllImport("ole32.dll", CharSet = CharSet.Unicode)]
    private static extern int CoCreateInstance(
        ref Guid rclsid,
        IntPtr pUnkOuter,
        uint dwClsContext,
        ref Guid riid,
        out IntPtr ppv);

    [DllImport("ole32.dll")]
    private static extern int CoInitializeEx(IntPtr pvReserved, uint dwCoInit);

    [DllImport("msctf.dll")]
    private static extern int TF_CreateCategoryMgr(out IntPtr ppCat);

    // COINIT_APARTMENTTHREADED = 0；TSF profiles 对象线程模型为 Apartment。
    private const uint COINIT_APARTMENTTHREADED = 0;
}
