using Cultiway.Abstract;
using Friflo.Engine.ECS.Systems;
using UnityEngine;

namespace Cultiway.Core;

/// <summary>按固定秒间隔执行的低频逻辑系统基类。</summary>
public abstract class ThrottledSystem : BaseSystem, IWorldStateClearable
{
    /// <summary>距离下一次执行的剩余秒数。</summary>
    private float cooldown;

    /// <summary>两次执行之间的最小间隔秒数。</summary>
    protected abstract float IntervalSeconds { get; }

    /// <summary>到达设定间隔时执行一次业务更新。</summary>
    protected abstract void OnThrottledUpdate();

    /// <summary>世界切换时清理派生系统持有的世界状态。</summary>
    protected virtual void OnThrottleWorldStateCleared()
    {
    }

    /// <summary>世界切换时重置节流计时，并通知派生系统清理自己的状态。</summary>
    void IWorldStateClearable.ClearWorldState()
    {
        cooldown = 0f;
        OnThrottleWorldStateCleared();
    }

    /// <summary>累计帧时长，攒够间隔后执行一次业务更新。</summary>
    protected override void OnUpdateGroup()
    {
        base.OnUpdateGroup();
        cooldown -= Mathf.Max(0f, Tick.deltaTime);
        if (cooldown > 0f) return;
        cooldown = Mathf.Max(0f, IntervalSeconds);
        OnThrottledUpdate();
    }
}
