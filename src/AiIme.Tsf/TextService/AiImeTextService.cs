using System.Runtime.InteropServices;
using AiIme.Core;
using AiIme.Core.Composition;
using AiIme.Tsf.Com;

namespace AiIme.Tsf.TextService;

/// <summary>
/// TSF 文本服务 COM 实现。系统通过 CoCreateInstance 创建本类，调用 Activate/Deactivate。
///
/// 当前阶段：
/// - Activate 保存线程管理器指针、组装 Core 引擎，并挂接按键 sink；
/// - 按键经 <see cref="CompositionState"/> 累积拼音并召回排序；
/// - composition 拼音串上屏、候选窗、语言栏在后续步骤接入。
/// </summary>
[ComVisible(true)]
[Guid("8f3c2a10-7d44-4c9b-9e2a-1b6d5a7c8f90")]
[ClassInterface(ClassInterfaceType.None)]
public sealed class AiImeTextService : ITfTextInputProcessor
{
    private IntPtr _threadMgr;
    private uint _clientId;
    private EngineBundle? _engine;
    private CompositionState? _composition;
    private CompositionComposer? _composer;
    private KeyEventSink? _keySink;
    private IntPtr _keystrokeMgr;
    private bool _keySinkAdvised;
    private bool _activated;

    /// <summary>TSF 激活文本服务时调用。</summary>
    public int Activate(IntPtr ptim, uint tid)
    {
        if (_activated) return HResult.S_OK;

        _threadMgr = ptim;
        _clientId = tid;
        TsfDebugLog.Write("Activate", $"threadMgr={ptim} clientId={tid}");
        TsfDebugLog.Write("Activate", $"BaseDirectory=[{AppContext.BaseDirectory}] ProcName={Environment.ProcessPath}");

        try
        {
            // 二分排查：v9 恢复引擎 + 实例化 KeyEventSink（CCW 对象），
            // 但不挂接到 KeystrokeMgr，确认 sink 对象构造/包装本身是否触发崩溃。
            var anchorDir = Path.GetDirectoryName(typeof(AiImeTextService).Assembly.Location);
            TsfDebugLog.Write("Activate", "step: create engine");
            _engine = EngineFactory.Create(anchorDir);
            TsfDebugLog.Write("Activate", "step: create composition");
            _composition = new CompositionState(_engine.Dictionary, _engine.Ranker);
            TsfDebugLog.Write("Activate", "step: create composer");
            _composer = new CompositionComposer(tid);
            TsfDebugLog.Write("Activate", "step: create keySink");
            _keySink = new KeyEventSink(_composition, _composer, _engine.Learner, tid);
            TsfDebugLog.Write("Activate", "step: advise key sink");

            AdviseKeySink();
            TsfDebugLog.Write("Activate", "engine/composer/keySink ready");

            _activated = true;
            return HResult.S_OK;
        }
        catch (Exception ex)
        {
            // 引擎初始化失败：降级为不拦截任何按键，避免在宿主进程内崩溃。
            var inner = ex.InnerException == null ? "" : $" | inner: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}";
            TsfDebugLog.Write("Activate", $"failed: {ex.GetType().Name}: {ex.Message}{inner}{Environment.NewLine}{ex.StackTrace}");
            Cleanup();
            return HResult.S_OK;
        }
    }

    /// <summary>TSF 停用文本服务时调用，注销 sink 并释放线程管理器引用。</summary>
    public int Deactivate()
    {
        TsfDebugLog.Write("Deactivate", "");
        if (!_activated) return HResult.S_OK;
        Cleanup();
        return HResult.S_OK;
    }

    private void AdviseKeySink()
    {
        if (_threadMgr == IntPtr.Zero || _keySink == null) return;

        // ITfKeyEventSink 通过 ITfKeystrokeMgr 挂接（不是 ITfSource::AdviseSink）。
        var mgrIid = TsfGuids.IID_ITfKeystrokeMgr;
        var qResult = Marshal.QueryInterface(_threadMgr, ref mgrIid, out _keystrokeMgr);
        TsfDebugLog.Write("AdviseKeySink", $"QI ITfKeystrokeMgr HRESULT=0x{qResult:X8} ptr={_keystrokeMgr}");
        if (qResult != HResult.S_OK || _keystrokeMgr == IntPtr.Zero)
        {
            TsfDebugLog.Write("AdviseKeySink", $"QueryInterface ITfKeystrokeMgr failed HRESULT=0x{qResult:X8}");
            _keystrokeMgr = IntPtr.Zero;
            return;
        }

        // 传 ITfKeyEventSink 专用接口指针（而非 IUnknown*）：原生按 sink vtable 调槽位，
        // 若传 IUnknown 指针，其 vtable 只有 3 个槽，原生取后续槽位会跳到垃圾地址崩溃。
        var sinkPtr = Marshal.GetComInterfaceForObject(_keySink, typeof(ITfKeyEventSink));
        TsfDebugLog.Write("AdviseKeySink", $"sink(ITfKeyEventSink) ptr={sinkPtr} tid={_clientId}");
        try
        {
            // 直接走原生 vtable 委托调用，绕过 RCW 出站封送。
            var advise = KeystrokeMgrNative.GetAdvise(_keystrokeMgr);
            // fforeground=1：前台上下文接收按键。
            int hr = advise(_keystrokeMgr, _clientId, sinkPtr, 1);
            TsfDebugLog.Write("AdviseKeySink", $"AdviseKeyEventSink HRESULT=0x{hr:X8}");
            _keySinkAdvised = hr == HResult.S_OK;
            if (!_keySinkAdvised)
            {
                TsfDebugLog.Write("AdviseKeySink", "AdviseKeyEventSink 未成功，按键 sink 未生效");
            }
        }
        catch (Exception ex)
        {
            TsfDebugLog.Write("AdviseKeySink", $"exception: {ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            Marshal.Release(sinkPtr);
        }
    }

    private void UnadviseKeySink()
    {
        if (_keystrokeMgr != IntPtr.Zero && _keySinkAdvised)
        {
            try
            {
                var unadvise = KeystrokeMgrNative.GetUnadvise(_keystrokeMgr);
                unadvise(_keystrokeMgr, _clientId);
            }
            catch (Exception ex)
            {
                TsfDebugLog.Write("UnadviseKeySink", $"exception: {ex.GetType().Name}: {ex.Message}");
            }
        }
        _keySinkAdvised = false;
        if (_keystrokeMgr != IntPtr.Zero)
        {
            Marshal.Release(_keystrokeMgr);
            _keystrokeMgr = IntPtr.Zero;
        }
    }

    private void Cleanup()
    {
        UnadviseKeySink();
        _composer?.Dispose();
        _keySink?.DisposeCandidateWindow();
        _keySink = null;
        _composition = null;
        _composer = null;
        _engine = null;
        _threadMgr = IntPtr.Zero;
        _clientId = 0;
        _activated = false;
    }

    /// <summary>当前 composition 状态（未激活时为 null）。</summary>
    internal CompositionState? Composition => _composition;

    /// <summary>已组装的 Core 引擎（未激活时为 null）。</summary>
    internal EngineBundle? Engine => _engine;
}
