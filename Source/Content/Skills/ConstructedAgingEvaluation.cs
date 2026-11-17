using System;
using System.Linq;
using Cultiway.Content.Components;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Core.Skills;
using Cultiway.Core.SkillLibV3.Effects;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;

namespace Cultiway.Content.Skills;

/// <summary>自主选择只读取当前原人物、装备和库存，不提前提交任何损耗。</summary>
public sealed partial class ConstructedAbilityService
{
    /// <summary>只有明确损耗且没有其他既有用途时，才按当前可推进内容排除候选。</summary>
    /// <param name="plan">同一实际计划。</param>
    private static bool IsPureAging(CompiledAbility plan) => plan.ActorImpacts.Any(impact => impact.HasAgingEffect) &&
        !plan.ActorImpacts.Any(impact => impact.HasOtherActorEffect) && !plan.HasBarrier &&
        !plan.HasWorldEffects && !plan.HasWorldMaterials && !plan.HasRecovery && plan.ContactControl == 0;

    /// <summary>先检查实际关系，再估算可推进份额，数值只表示用途，不作为伤害承诺。</summary>
    /// <param name="caster">真实来源。</param><param name="plan">已经展开的作用。</param>
    /// <param name="target">当前战斗目标。</param>
    private static float AgingCombatValue(ActorExtend caster, CompiledAbility plan, BaseSimObject target)
    {
        if (target?.isActor() != true || !target.a.isAlive() || !target.a.hasHealth()) return 0f;
        Actor actor = target.a;
        float value = 0;
        foreach (var impact in plan.ActorImpacts)
        {
            if (!impact.HasAgingEffect || !SkillTargetRelationResolver.Matches(impact.Targets, caster.Base, actor)) continue;
            if (impact.AgeYears > 0 && WorldLawLibrary.world_law_old_age.isEnabled() &&
                !actor.hasTrait("immortal") && actor.stats["lifespan"] > 0)
            {
                double years = Math.Min(impact.AgeYears,
                    Math.Min((long)int.MaxValue - actor.getAge(), (long)int.MaxValue - actor.data.age_overgrowth));
                value = Math.Max(value, (float)(100d * years / actor.stats["lifespan"]));
            }
            if (impact.EquipmentErosion != null && actor.equipment != null)
                foreach (EquipmentType slot in impact.EquipmentErosion.Slots)
                {
                    Item item = actor.equipment.getSlot(slot).getItem();
                    if (item == null || !item.isAlive() || item.getActor() != actor || item.isBroken() || item.hasMod("eternal")) continue;
                    int loss = EquipmentErosionDamage(item, impact.EquipmentErosion.Fraction, 1f);
                    if (loss > 0) value = Math.Max(value, 100f * loss / item.getDurabilityMax());
                }
            if (impact.InventoryAging != null)
                value = Math.Max(value, InventoryAgingValue(actor.GetExtend(), impact.InventoryAging));
        }
        return value;
    }

    /// <summary>沿实际库存顺序估算有限件数，可表示进度与真实提交共用同一计算。</summary>
    /// <param name="owner">当前原持有人。</param><param name="effect">已编译寿命声明。</param>
    private static float InventoryAgingValue(ActorExtend owner, CompiledInventoryAging effect)
    {
        float value = 0; int accepted = 0;
        foreach (Entity item in owner.GetItems())
        {
            if (accepted >= effect.MaximumItems) break;
            if (!CanAdvanceInventoryLife(item) || !item.GetIncomingLinks<InventoryRelation>().Entities.Contains(owner.E) ||
                effect.Shapes.Count > 0 && !effect.Shapes.Contains(item.GetComponent<ItemShape>().shape_id)) continue;
            float before = item.GetComponent<AliveTimer>().value, limit = item.GetComponent<AliveTimeLimit>().value;
            float after = InventoryAgingEnd(before, limit, effect.Seconds);
            if (after <= before) continue;
            value += 100f * (after - before) / limit; accepted++;
        }
        return value;
    }
}
