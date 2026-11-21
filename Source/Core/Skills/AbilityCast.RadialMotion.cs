using UnityEngine;

namespace Cultiway.Core.Skills;

public sealed partial class AbilityCast
{
    /// <summary>范围运动只使用当前真实存在的部位；结束或等待生成时没有范围作用。</summary>
    /// <param name="node">实际部位。</param><param name="centre">本步已经确认的世界中心。</param>
    internal bool TryRadialMotion(int node, out Vector3 centre)
    {
        ref NodeState state = ref nodes[node];
        CompiledForm form = Plan.Nodes[node];
        centre = state.Transform.GetColumn(3);
        float age = NodeAge(node);
        return !Finished && state.Exists && state.Started && age >= form.Presence.x && age < form.Presence.y &&
            form.RadialMotion != null && age >= form.RadialMotion.StartAt && form.RadialMotion.Speed != 0f;
    }

    /// <summary>地口、共同受力和出口都从同一步部位姿态取样。</summary>
    internal bool TryWindChannel(int node, out ConstructionWindChannel channel)
    {
        CompiledForm form = Plan.Nodes[node];
        channel = default;
        if (form.RadialMotion is not { ChannelLength: > 0f } rule || !TryRadialMotion(node, out _)) return false;
        float progress = (NodeAge(node) - form.Presence.x) / (form.Presence.y - form.Presence.x);
        channel = new ConstructionWindChannel(nodes[node].Transform, rule, progress);
        return true;
    }
}
