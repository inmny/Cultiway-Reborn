using System;
using System.Linq;
using Cultiway.Core.Skills;
using UnityEngine;

namespace Cultiway.Content.Skills;

/// <summary>原物品已经发生的腐蚀结果，耐久和装备归属仍由原玩法持有。</summary>
public readonly struct ConstructedEquipmentErosion
{
    /// <summary>本次施放、原来源、当前归属、实际持有人和原物品。</summary>
    public readonly long CastId, SourceId, OwnerId, TargetId, ItemId;
    /// <summary>本次构造及原装备资产声明的材料。</summary>
    public readonly string AbilityId, Matter;
    /// <summary>真实作用部位及前后耐久。</summary>
    public readonly int Node, Before, After;
    /// <summary>实际持有物品的明确装备栏。</summary>
    public readonly EquipmentType Slot;
    /// <summary>本次确认后的原版破损状态。</summary>
    public readonly bool Broken;
    /// <summary>已经确认接触的共同世界时点。</summary>
    public readonly double WorldTime;

    /// <summary>只记录已经发生的耐久变化。</summary>
    /// <param name="cast">实际施放。</param><param name="source">原来源。</param><param name="owner">当前归属。</param>
    /// <param name="target">实际人物。</param><param name="item">原物品。</param><param name="ability">正式构造。</param>
    /// <param name="matter">装备实际材料。</param><param name="node">作用部位。</param><param name="slot">明确装备栏。</param>
    /// <param name="before">原耐久。</param><param name="after">剩余耐久。</param><param name="broken">原版破损状态。</param>
    /// <param name="time">共同世界时点。</param>
    internal ConstructedEquipmentErosion(long cast, long source, long owner, long target, long item, string ability,
        string matter, int node, EquipmentType slot, int before, int after, bool broken, double time)
    {
        CastId = cast; SourceId = source; OwnerId = owner; TargetId = target; ItemId = item;
        AbilityId = ability; Matter = matter; Node = node; Slot = slot;
        Before = before; After = after; Broken = broken; WorldTime = time;
    }
}

/// <summary>真实人物接触只腐蚀明确栏位中的原物品，不建立物品副本。</summary>
public sealed partial class ConstructedAbilityService
{
    /// <summary>原物品已经接受真实耐久变化。</summary>
    public static event Action<ConstructedEquipmentErosion> EquipmentErosionConfirmed;

    /// <summary>依次确认实际归属、材料和可表示损耗，再由原版受损及属性入口提交。</summary>
    /// <param name="running">实际施放。</param><param name="node">作用部位。</param><param name="target">已确认存活人物。</param>
    /// <param name="effect">编译后的明确装备栏。</param><param name="result">真实攻击结果。</param>
    /// <param name="strength">接触或传播的衰减。</param>
    /// <param name="contact">直接相交位置，或空表示由介质传到。</param>
    /// <returns>仍为同一世界的原存活人物时返回真。</returns>
    private static bool ApplyEquipmentErosion(RunningCast running, int node, Actor target,
        CompiledEquipmentErosion effect, ApplyAttackState result, float strength, Vector3? contact)
    {
        if (effect == null || result != ApplyAttackState.Hit || target.equipment == null) return true;
        long epoch = worldGeneration, targetId = target.id, owner = running.Owner(node).Id;
        foreach (EquipmentType slot in effect.Slots)
        {
            Item item = target.equipment.getSlot(slot).getItem();
            if (item == null || !item.isAlive() || item.getActor() != target || item.isBroken()) continue;
            string matter = item.getAsset().material;
            int before = item.getDurabilityCurrent();
            int damage = EquipmentErosionDamage(item, effect.Fraction, strength);
            if (damage <= 0) continue;
            item.getDamaged(damage);
            int after = item.getDurabilityCurrent();
            if (after == before) continue;
            if (node >= 0 && running.Cast.Matter(node).Id == "wind" && contact is Vector3 point)
                AddWindScar(running, node, target, point, true);
            target.setStatsDirty();
            target.updateStats();
            EquipmentErosionConfirmed?.Invoke(new ConstructedEquipmentErosion(running.Id, running.SourceId, owner,
                targetId, item.id, running.Cast.Plan.Id, matter, node, slot, before, after, item.isBroken(), simulation.contactTime));
            if (epoch != worldGeneration || target.data?.id != targetId || !target.isAlive() || !target.hasHealth()) return false;
        }
        return true;
    }

    /// <summary>实际提交与自主估算使用相同材料和整数损耗。</summary>
    /// <param name="item">当前原物品。</param><param name="fraction">明确份额。</param>
    /// <param name="strength">实际接触倍率，自主估算采用一。</param>
    private static int EquipmentErosionDamage(Item item, float fraction, float strength)
    {
        string matter = item.getAsset().material;
        string rule = matter == "stone" ? "earth" : matter;
        float factor = AbilityMatters.All.FirstOrDefault(value => value.Id == rule && value.State == "solid")?.ErosionFactor ?? 0f;
        return (int)Math.Floor(Math.Min(item.getDurabilityCurrent(),
            item.getDurabilityMax() * (double)fraction * factor * strength));
    }
}
