using System;
using Cultiway.Core.Skills;
using UnityEngine;

namespace Cultiway.Content.Skills;

/// <summary>已经发生的原装备修复及实际花费的材料。</summary>
public readonly struct ConstructedEquipmentRepair
{
    /// <summary>本次施放、材料提供者、装备持有人和原装备。</summary>
    public readonly long CastId, SourceId, TargetId, ItemId;
    /// <summary>实际装备栏。</summary>
    public readonly EquipmentType Slot;
    /// <summary>修复前后的耐久。</summary>
    public readonly int Before, After;
    /// <summary>消耗的原材料的稳定编号。</summary>
    public readonly string MaterialId;
    /// <summary>真实接触时点。</summary>
    public readonly double WorldTime;

    /// <summary>记录已经发生的变化。</summary>
    /// <param name="cast">本次施放。</param><param name="source">原来源。</param>
    /// <param name="target">装备持有人。</param><param name="item">原装备。</param>
    /// <param name="slot">装备栏。</param><param name="before">原耐久。</param><param name="after">当前耐久。</param>
    /// <param name="material">实际材料。</param><param name="time">实际接触时点。</param>
    internal ConstructedEquipmentRepair(long cast, long source, long target, long item,
        EquipmentType slot, int before, int after, string material, double time)
    {
        CastId = cast; SourceId = source; TargetId = target; ItemId = item;
        Slot = slot; Before = before; After = after; MaterialId = material; WorldTime = time;
    }
}

/// <summary>通过已经确认的接触修复原装备。</summary>
public sealed partial class ConstructedAbilityService
{
    /// <summary>实际消耗一件材料并恢复了原装备耐久之后发布。</summary>
    public static event Action<ConstructedEquipmentRepair> EquipmentRepairConfirmed;

    /// <summary>先算出真实修复量，再消费材料和修改原装备。</summary>
    /// <param name="running">实际施放。</param><param name="node">作用部位。</param><param name="target">已确认的实际人物。</param>
    /// <param name="effect">编译后的修复规则。</param><param name="result">真实接触结果。</param>
    /// <param name="strength">接触或传播的衰减。</param>
    /// <returns>仍为同一世界的原存活人物时返回真。</returns>
    private static bool ApplyEquipmentRepair(RunningCast running, int node, Actor target,
        CompiledEquipmentRepair effect, ApplyAttackState result, float strength)
    {
        if (effect == null || result != ApplyAttackState.Hit || target.equipment == null || running.Inventory == null)
            return true;
        CastAuthority owner = running.Owner(node);
        if (!owner.IsLive || owner.Id != running.SourceId || owner.Actor != running.Source) return true;
        Item item = target.equipment.getSlot(effect.Slot).getItem();
        if (item == null || !item.isAlive() || item.getActor() != target) return true;
        int before = item.getDurabilityCurrent();
        int repair = Math.Min(item.getDurabilityMax() - before, Mathf.FloorToInt(effect.Durability * Mathf.Clamp01(strength)));
        if (repair <= 0 || !running.Inventory.TryConsumeForRepair(effect.MaterialInput, out string material)) return true;

        long epoch = worldGeneration, targetId = target.id;
        int after = before + repair;
        item.data.durability = after;
        target.setStatsDirty();
        target.updateStats();
        EquipmentRepairConfirmed?.Invoke(new ConstructedEquipmentRepair(running.Id, owner.Id, targetId, item.id,
            effect.Slot, before, after, material, simulation.contactTime));
        return epoch == worldGeneration && target.data?.id == targetId && target.isAlive() && target.hasHealth();
    }
}
