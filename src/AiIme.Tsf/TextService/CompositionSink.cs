using System.Runtime.InteropServices;
using AiIme.Tsf.Com;

namespace AiIme.Tsf.TextService;

/// <summary>
/// 最小 composition 终止回调：composition 被宿主/外部终止时清空协调器持有的区间引用。
/// 作为 CCW 对象传给 TSF，生命周期由 <see cref="CompositionComposer"/> 持有。
/// </summary>
[ComVisible(true)]
[ClassInterface(ClassInterfaceType.None)]
internal sealed class CompositionSink : ITfCompositionSink
{
    private readonly CompositionComposer _owner;

    public CompositionSink(CompositionComposer owner)
    {
        _owner = owner;
    }

    public int OnCompositionTerminated(uint ecWrite, IntPtr pComposition)
    {
        TsfDebugLog.Write("CompositionSink", $"OnCompositionTerminated ec={ecWrite} composition={pComposition} current={_owner.IsCurrentComposition(pComposition)}");
        // 只有被终止的就是当前持有区间时才清理；正常/异步结束的回调直接忽略。
        if (_owner.IsCurrentComposition(pComposition))
        {
            _owner.ClearComposition();
        }
        return HResult.S_OK;
    }
}
