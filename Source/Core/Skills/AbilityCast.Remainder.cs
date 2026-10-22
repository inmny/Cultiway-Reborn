using UnityEngine;

namespace Cultiway.Core.Skills;

/// <summary>正常结束时将实际余力一次性交接到独立后段，数值快照自然包含全部结果。</summary>
public sealed partial class AbilityCast
{
    /// <summary>当前身份已经冻结在哪个原部位，内容层仍负责真正人物及伤害归属。</summary>
    /// <param name="node">已经展开的部位。</param>
    internal int AuthorityNode(int node) => nodes[node].Authority;

    /// <summary>先统计仍可接收的权重，再守恒分配和复制事实，最后清空来源。</summary>
    /// <param name="node">真正结束且只通知一次的来源。</param>
    private void TransferRemainder(int node)
    {
        int[] receivers = Plan.Nodes[node].RemainderReceivers;
        double total = 0; int left = 0;
        foreach (int index in receivers)
            if (nodes[index].Exists && float.IsPositiveInfinity(nodes[index].BirthAt))
            { total += Plan.Nodes[index].Strength; left++; }
        if (left == 0) return;
        NodeState source = nodes[node]; float remaining = source.Force; double volumeLeft = source.Volume, resourceLeft = source.Resource;
        foreach (int index in receivers)
        {
            if (!nodes[index].Exists || !float.IsPositiveInfinity(nodes[index].BirthAt)) continue;
            float share = left == 1 ? remaining : Mathf.Min(remaining, (float)(source.Force * (Plan.Nodes[index].Strength / total)));
            double volume = left == 1 ? volumeLeft : System.Math.Min(volumeLeft, source.Volume * (Plan.Nodes[index].Strength / total));
            volumeLeft -= volume; remaining = Mathf.Max(0f, remaining - share); left--;
            double resource = left == 0 ? resourceLeft : System.Math.Min(resourceLeft, source.Resource * (Plan.Nodes[index].Strength / total));
            resourceLeft -= resource; TransferBranch(index, share, volume, resource, in source);
        }
        nodes[node].Force = 0f; nodes[node].Volume = 0; nodes[node].Resource = 0;
    }

    /// <summary>分支内部仍按真实作用部位分配，嵌套子结构不能重新获得初始力量。</summary>
    /// <param name="root">明确接收的分支根。</param>
    /// <param name="amount">该分支已经取得的真实份额。</param>
    /// <param name="volume">实际体积份额。</param>
    /// <param name="resource">未耗支付份额，与作用量及体积分别计量。</param>
    /// <param name="source">结束时冻结的实际来源。</param>
    private void TransferBranch(int root, float amount, double volume, double resource, in NodeState source)
    {
        double total = 0; int left = 0;
        foreach (int index in Plan.Nodes[root].RemainderMembers)
            if (nodes[index].Exists && Plan.Nodes[index].ContactRole != ConstructionContactRole.Decoration)
            { total += Plan.Nodes[index].Strength; left++; }
        float remaining = amount; double volumeLeft = volume, resourceLeft = resource;
        foreach (int index in Plan.Nodes[root].RemainderMembers)
        {
            ref NodeState target = ref nodes[index]; if (!target.Exists) continue;
            float share = 0f;
            if (Plan.Nodes[index].ContactRole != ConstructionContactRole.Decoration)
            {
                share = left == 1 ? remaining : Mathf.Min(remaining, (float)(amount * (Plan.Nodes[index].Strength / total)));
                double pieceVolume = left == 1 ? volumeLeft : System.Math.Min(volumeLeft, volume * (Plan.Nodes[index].Strength / total)); target.Volume = pieceVolume; volumeLeft -= pieceVolume;
                remaining = Mathf.Max(0f, remaining - share); left--;
            }
            target.Resource = Plan.Nodes[index].ContactRole == ConstructionContactRole.Decoration ? 0 : left == 0 ? resourceLeft : System.Math.Min(resourceLeft, resource * (Plan.Nodes[index].Strength / total));
            resourceLeft -= target.Resource;
            target.Force = target.InitialForce = share;
            target.Matter = source.Matter;
            target.ShapeHeld = source.ShapeHeld;
            target.Burning = source.Burning;
            target.Burning.Retain(source.Volume > 0 ? target.Volume / source.Volume : 0);
            target.Authority = source.Authority;
            target.Reflections = source.Reflections;
            target.HasTarget = source.HasTarget;
            target.Target = source.Target;
            target.Velocity = source.Free ? source.Velocity : source.AttachedVelocity;
        }
    }
}
