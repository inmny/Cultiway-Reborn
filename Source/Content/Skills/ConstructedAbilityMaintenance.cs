using System;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Const;
using Cultiway.Content.Combat;
using Cultiway.Core.Combat.Tactical;
using Cultiway.Core.Skills;
using Cultiway.Core.SkillLibV3.ActiveAbilities;
using Cultiway.Core.SkillLibV3.Usage;
using Cultiway.Utils.Extension;
using UnityEngine;

namespace Cultiway.Content.Skills;

public sealed partial class ConstructedAbilityService
{
    /// <summary>在原版维修任务中只尝试一次能立即接触本人当前装备的已学修复术。</summary>
    /// <param name="actor">正在开始维修的真实人物。</param>
    internal static bool TryOwnEquipmentRepair(Actor actor)
    {
        if (!GeneralSettings.EnableSkillSystems || actor.equipment == null || actor.current_tile == null) return false;
        var task = actor.ai.task;
        var tile = actor.current_tile;
        long actorId = actor.id, generation = worldGeneration;
        bool CanRepair() => generation == worldGeneration && actor.data?.id == actorId && actor.isAlive() && actor.hasHealth() &&
                            actor.ai.task == task && actor.current_tile == tile && actor.position_height <= 0f &&
                            !actor.under_forces && !actor.isInMagnet() && !actor.hasTag("frozen_ai") &&
                            !ControllableUnit.isControllingUnit(actor) && !BodyControlState.IsBusy(actorId) &&
                            actor.attack_target.isRekt() && !CombatWorldService.IsEngaged(actor);
        if (!CanRepair()) return false;
        var owner = actor.GetExtend();
        if (!owner.E.TryGetComponent(out ConstructedKnowledge known) || known.Ids.Length == 0) return false;
        StartingPose(actor, out Vector3 foot, out float height);
        var body = ActorHitBoundary.Create(foot, height, Vector3.zero);
        var contacts = new List<AbilityCapsule>();
        int offset = (int)((actorId + (long)World.world.getCurWorldTime()) % known.Ids.Length);
        for (int i = 0; i < Math.Min(8, known.Ids.Length); i++)
        {
            string ability = known.Ids[(offset + i) % known.Ids.Length];
            if (!definitions.TryGetValue(ability, out var definition) || definition.Power <= 0f) continue;
            CompiledAbility plan = PlanFor(ability);
            CompiledEquipmentRepair repair = OwnEquipmentRepairRule(plan);
            if (repair == null) continue;
            Item Equipped() => actor.equipment?.getSlot(repair.Slot).getItem();
            Item item = Equipped();
            if (item == null || !item.isAlive() || item.getActor() != actor || !item.needRepair()) continue;
            long itemId = item.id;
            bool Valid() => CanRepair() && item.isAlive() && item.id == itemId && Equipped() == item &&
                            item.getActor() == actor && item.needRepair();
            var scope = new ConstructionWorldScope(Valid, (_, _) => false);
            var target = new ActiveAbilityTarget(actor, foot, worldScope: scope);
            var handle = new ActiveAbilityHandle(ProviderId, owner.E, ability);
            if (!ActiveAbilityService.CanUse(owner, handle, target)) continue;

            // 只为起步就能碰到本人的有效修复部位付款，不按模型大小或预计飞行路线猜测。
            var preview = new AbilityCast(plan, Matrix4x4.Translate(foot), inputs: new AbilityInputs(foot, foot, Vector3.right, height));
            contacts.Clear();
            preview.AppendContacts(contacts);
            bool reaches = contacts.Any(form => form.Node >= 0 && preview.AffectsActors(form.Node) &&
                preview.Impact(form.Node).EquipmentRepair == repair &&
                plan.ContactPolicies[plan.Nodes[form.Node].ContactGroup].Mode != ConstructionContactMode.Pulse &&
                AbilityPairSweep.Find(form, form, body, body, out _, out _, out _));
            if (reaches && ActiveAbilityService.TryUse(owner, handle, target, ActiveAbilityUseOrigin.Autonomous)) return true;
        }
        return false;
    }

    /// <summary>只接受不会连带伤害、移动本人或修改地块的单份修复。</summary>
    /// <param name="plan">已登记的普通构造。</param>
    private static CompiledEquipmentRepair OwnEquipmentRepairRule(CompiledAbility plan)
    {
        if ((plan.Use.Channels & ActiveAbilityChannel.World) == 0 ||
            plan.Use.Target is not (ActiveAbilityTargetMode.Self or ActiveAbilityTargetMode.Object or ActiveAbilityTargetMode.ObjectOrPoint) ||
            plan.Binding?.Anchor is not (AbilityAnchor.Source or AbilityAnchor.Target) || plan.Body != null ||
            plan.HasBarrier || plan.HasWorldEffects || plan.HasWorldMaterials || plan.HasRecovery || plan.HasCorrespondences ||
            plan.FormCount > 16 || plan.Inventory.Length != 1 || plan.Inventory[0].Count != 1 ||
            plan.Nodes.Any(node => node.RadialMotion is { Speed: not 0f } || node.Matter.Id == "fire" && node.Matter.State == "energy") ||
            plan.EventOperations.Any(operation => operation.Kind != ConstructionOperationKind.Animate)) return null;
        CompiledEquipmentRepair repair = null;
        foreach (var impact in plan.ActorImpacts)
        {
            if (!impact.HasActorEffect) continue;
            if (impact.EquipmentRepair == null || repair != null || impact.HasAgingEffect ||
                impact.HasActorEffectBesidesStatusesAndRepair || impact.Statuses.Count != 0 ||
                (impact.Targets != SkillUseTargetRelation.Self &&
                 (impact.Targets != SkillUseTargetRelation.Friendly || !impact.SelectedTargetOnly))) return null;
            repair = impact.EquipmentRepair;
        }
        return repair?.MaterialInput == plan.Inventory[0].Id ? repair : null;
    }
}
