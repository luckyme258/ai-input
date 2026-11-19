using System.Reflection;
using System.Runtime.InteropServices;

// 对照探针：空 ITfKeyEventSink（无 Core 依赖）挂到系统 KeystrokeMgr，判断是否崩溃。
internal static class Probe
{
    private static void Main()
    {
        var emptySink = new EmptyKeySink();
        Console.WriteLine("empty sink created");

        var iidThreadMgr = new Guid("aa80e801-2021-11d2-93e0-0060b067b86e");
        var clsidThreadMgr = new Guid("529a9e6b-6587-4f23-ab9e-9c7d683e3c50");
        int hrCo = CoCreateInstance(ref clsidThreadMgr, IntPtr.Zero, 1, ref iidThreadMgr, out var pThreadMgr);
        Console.WriteLine($"CoCreateInstance(ThreadMgr) HRESULT=0x{hrCo:X8} ptr={pThreadMgr}");
        if (hrCo != 0 || pThreadMgr == IntPtr.Zero) return;

        var activate = Marshal.GetDelegateForFunctionPointer<ActivateDelegate>(
            Marshal.ReadIntPtr(Marshal.ReadIntPtr(pThreadMgr), IntPtr.Size * 3));
        int hrAct = activate(pThreadMgr, out uint clientId);
        Console.WriteLine($"ThreadMgr.Activate HRESULT=0x{hrAct:X8} clientId={clientId}");

        var iidMgr = new Guid("aa80e7f0-2021-11d2-93e0-0060b067b86e");
        int hrQ = Marshal.QueryInterface(pThreadMgr, ref iidMgr, out var pMgr);
        Console.WriteLine($"QI ITfKeystrokeMgr HRESULT=0x{hrQ:X8} ptr={pMgr}");
        if (hrQ != 0) return;

        var vtbl = Marshal.ReadIntPtr(pMgr);
        var adviseFn = Marshal.GetDelegateForFunctionPointer<AdviseDelegate>(
            Marshal.ReadIntPtr(vtbl, IntPtr.Size * 3));

        var sinkPtr = Marshal.GetIUnknownForObject(emptySink);
        Console.WriteLine($"sink(IUnknown) ptr={sinkPtr}");
        int hrAdv = adviseFn(pMgr, clientId, sinkPtr, 1);
        Console.WriteLine($"AdviseKeyEventSink(empty sink) HRESULT=0x{hrAdv:X8}");
        Marshal.Release(sinkPtr);
        Marshal.Release(pMgr);
        Marshal.Release(pThreadMgr);
    }

    [ComImport, Guid("aa80e7f5-2021-11d2-93e0-0060b067b86e"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IEmptySink
    {
        [PreserveSig] int OnSetFocus(int fforeground);
        [PreserveSig] int OnTestKeyDown(IntPtr pic, IntPtr w, IntPtr l, out int eaten);
        [PreserveSig] int OnTestKeyUp(IntPtr pic, IntPtr w, IntPtr l, out int eaten);
        [PreserveSig] int OnKeyDown(IntPtr pic, IntPtr w, IntPtr l, out int eaten);
        [PreserveSig] int OnKeyUp(IntPtr pic, IntPtr w, IntPtr l, out int eaten);
        [PreserveSig] int OnPreservedKey(IntPtr pic, ref Guid rguid, out int eaten);
    }

    public sealed class EmptyKeySink : IEmptySink
    {
        public int OnSetFocus(int f) => 0;
        public int OnTestKeyDown(IntPtr p, IntPtr w, IntPtr l, out int e) { e = 0; return 0; }
        public int OnTestKeyUp(IntPtr p, IntPtr w, IntPtr l, out int e) { e = 0; return 0; }
        public int OnKeyDown(IntPtr p, IntPtr w, IntPtr l, out int e) { e = 0; return 0; }
        public int OnKeyUp(IntPtr p, IntPtr w, IntPtr l, out int e) { e = 0; return 0; }
        public int OnPreservedKey(IntPtr p, ref Guid g, out int e) { e = 0; return 0; }
    }

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int ActivateDelegate(IntPtr self, out uint tid);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int AdviseDelegate(IntPtr self, uint tid, IntPtr psink, int ffg);

    [DllImport("ole32.dll")]
    private static extern int CoCreateInstance(ref Guid rclsid, IntPtr pUnkOuter, uint ctx, ref Guid riid, out IntPtr ppv);
}
