using UnityEngine;

namespace Cultiway.Core.Skills;

/// <summary>主线程确认真实选敌后，只将目标位置及实际余力交给普通数值部位。</summary>
public sealed partial class AbilityCast
{
    /// <summary>同一开始条件只接受一次目标，条件未满足时不提前改变分支。</summary>
    /// <param name="node">直接事件监听部位。</param><param name="source">真实接触来源。</param>
    internal bool AwaitsContactTarget(int node, int source)
    {
        if (node < 0) return false;
        var gate = Plan.Nodes[node].Activation;
        return gate?.Target != null && gate.Source == source && nodes[node].Exists &&
            !nodes[node].Started && !nodes[node].HasTarget && !float.IsPositiveInfinity(nodes[node].BirthAt);
    }

    /// <summary>有限后续部位使用来源当前余力；未找到目标时由调用方结束该分支。</summary>
    /// <param name="node">已确认可开始的后续部位。</param><param name="source">真实接触来源。</param><param name="target">已选中人物的实际位置。</param>
    internal void SelectContactTarget(int node, int source, Vector3 target)
    {
        ref NodeState state = ref nodes[node];
        state.Force = state.InitialForce = nodes[source].Force * Plan.Nodes[node].Activation.Target.ForceMultiplier;
        state.Reflections = nodes[source].Reflections;
        SetTarget(node, target);
        QueueBirth(node);
        FlushBirths(inputs.Target);
        UpdateSteps();
    }
}
