using UnityEngine;

namespace Cultiway.Core.Skills;

/// <summary>地块接触启动普通构造过程，不借助一次额外的地形修改。</summary>
public sealed partial class AbilityCast
{
    /// <summary>取得尚未接收的地块事件最早准备年龄，无监听者时不查询地面。</summary>
    /// <param name="source">实际作用部位，负一表示身体。</param>
    /// <param name="shape">这次查询的真实形状。</param>
    internal float WorldContactReadyAt(int source, int shape)
    {
        if (!Plan.HasWorldContactEvents || cancelled || source >= 0 &&
            Plan.Nodes[source].WorldContactShape >= 0 && Plan.Nodes[source].WorldContactShape != shape)
            return float.PositiveInfinity;
        float ready = float.PositiveInfinity;
        int bucket = (source < 0 ? nodes.Length : source) * CompiledConstructionCondition.EventCount +
                     (int)ConstructionChangeTrigger.WorldContact - 1;
        foreach (int index in Plan.EventListeners[bucket])
            ready = Mathf.Min(ready, index < 0 ? OperationEventReadyAt(~index) :
                nodes[index].Exists && float.IsPositiveInfinity(nodes[index].BirthAt) ? 0f : float.PositiveInfinity);
        return ready;
    }

    /// <summary>回到真实最早接触时点后，记录接触并开始已声明分支。</summary>
    /// <param name="source">实际作用部位，负一表示身体。</param>
    /// <param name="shape">已经确认的实际形状。</param>
    /// <param name="point">已确认的作用面接触点。</param>
    internal bool ConfirmWorldContact(int source, int shape, Vector3 point)
    {
        if (WorldContactReadyAt(source, shape) > Age) return false;
        RecordActionContact(source, point, AbilityActionContactKind.World);
        SignalEvent(source, ConstructionChangeTrigger.WorldContact, point);
        if (source >= 0) nodes[source].GroundSupported = nodes[source].MovementStopped;
        FlushBirths(inputs.Target);
        RecordActionTimeline();
        return true;
    }

    /// <summary>抓持期间仍须保持在原承力部位；仅仅发生过接地不保证支撑仍可用。</summary>
    /// <param name="node">使用支撑的部位。</param>
    internal bool HasGripSupport(int node)
    {
        int parent = Plan.Nodes[node].Parent;
        return !cancelled && parent >= 0 && !nodes[node].Free &&
               nodes[parent].Exists && nodes[parent].GroundSupported && nodes[parent].MovementStopped &&
               !nodes[parent].Recovering && !nodes[parent].Leaving && nodes[parent].Force > 0f;
    }

    /// <summary>收势时保留模型也可以结束人物占用。</summary>
    /// <param name="node">正在使用的实际抓持部位。</param>
    internal bool GripPartActive(int node) => nodes[node].Exists && nodes[node].Started &&
        !nodes[node].Recovering && !nodes[node].Leaving && nodes[node].Force > 0f && ProcessIsActive(node, includeEnd: false);
}
