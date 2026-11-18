using System;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Const;
using Cultiway.Content.Combat;
using Cultiway.Content.Components;
using Cultiway.Content.Libraries;
using Cultiway.Core.Combat.Tactical;
using Cultiway.Core.Skills;
using Cultiway.Core.SkillLibV3.ActiveAbilities;
using Cultiway.Core.SkillLibV3.Usage;
using Cultiway.Utils.Extension;
using UnityEngine;

namespace Cultiway.Content.Skills;

public sealed partial class ConstructedAbilityService
{
    /// <summary>完成普通一轮吐纳后，选一项真实可接触本人且有净收益的凝神构造。</summary>
    /// <param name="context">原持续修炼任务的结算及剩余时间。</param>
    /// <param name="tile">本次已实际抽取的地块。</param>
    /// <param name="supply">该地块剩余供给，已按原规则换算成灵气上限。</param>
    /// <param name="baseline">原方式每轮的未加成请求量。</param>
    internal static void TryMeditationFocus(in CultivationTriggerContext context, WorldTile tile, float supply, float baseline)
    {
        if (!GeneralSettings.EnableSkillSystems || supply <= baseline || context.ElapsedSeconds <= 0f) return;
        var owner = context.Practitioner;
        Actor actor = owner.Base;
        if (!owner.E.TryGetComponent(out ConstructedKnowledge known) || known.Ids.Length == 0) return;
        var task = context.Activity == CultivationActivityKind.EnvironmentalMeditation
            ? ActorTasks.DailyEnvironmentalCultivate : ActorTasks.DailyXianCultivate;
        long id = actor.id, generation = worldGeneration;
        bool Valid() => generation == worldGeneration && actor.data?.id == id && actor.isAlive() && actor.hasHealth() &&
                        actor.ai.task == task && actor.current_tile == tile && actor.position_height <= 0f &&
                        !actor.under_forces && !actor.isInMagnet() && !actor.hasTag("frozen_ai") &&
                        !ControllableUnit.isControllingUnit(actor) && !BodyControlState.IsBusy(id) &&
                        actor.attack_target.isRekt() && !CombatWorldService.IsEngaged(actor);
        if (!Valid()) return;
        float capacity = actor.stats[BaseStatses.MaxWakan.id] - owner.GetCultisys<Xian>().wakan;
        float limit = Mathf.Min(capacity, supply);
        if (limit <= baseline) return;

        StartingPose(actor, out Vector3 foot, out float height);
        var body = ActorHitBoundary.Create(foot, height, Vector3.zero);
        var scope = new ConstructionWorldScope(Valid, (_, _) => false);
        var target = new ActiveAbilityTarget(actor, foot, worldScope: scope);
        var contacts = new List<AbilityCapsule>();
        int offset = (int)((id + (long)World.world.getCurWorldTime()) % known.Ids.Length);
        for (int index = 0; index < Math.Min(8, known.Ids.Length); index++)
        {
            string ability = known.Ids[(offset + index) % known.Ids.Length];
            if (!definitions.TryGetValue(ability, out var definition) || definition.Power <= 0f ||
                definition.AdditionalResources.Length != 0 || !ConstructionResources.Same(definition.Resource, "wakan")) continue;
            CompiledAbility plan = PlanFor(ability);
            if (!MeditationFocusPlan(plan)) continue;
            float cost = ConstructionResources.Quote(owner, definition.Resource, definition.Cost);
            var handle = new ActiveAbilityHandle(ProviderId, owner.E, ability);
            if (!ActiveAbilityService.CanUse(owner, handle, target)) continue;

            // 只有起步时真的包住本人边界的部位可进入收益估计；不按视觉大小猜命中。
            var input = new AbilityInputs(foot, foot, Vector3.right, height);
            var preview = new AbilityCast(plan, Matrix4x4.Translate(foot), inputs: input);
            contacts.Clear();
            preview.AppendContacts(contacts);
            bool useful = false;
            foreach (var form in contacts)
            {
                if (form.Node < 0 || !preview.AffectsActors(form.Node) ||
                    !AbilityPairSweep.Find(form, form, body, body, out _, out _, out _)) continue;
                foreach (var focus in preview.Impact(form.Node).Statuses)
                {
                    float tick = context.ElapsedSeconds;
                    int ticks = Math.Min(Mathf.CeilToInt(context.RemainingActivitySeconds / tick),
                        Mathf.CeilToInt(focus.Duration / tick) - 1);
                    float unassisted = baseline * ticks;
                    float extra = Mathf.Min(limit, unassisted * (1f + Mathf.Clamp01(focus.Stats[BaseStatses.MeditationFocus.id]))) -
                                  Mathf.Min(limit, unassisted);
                    if (ticks > 0 && extra > cost + .001f) useful = true;
                }
                if (useful) break;
            }
            if (useful && ActiveAbilityService.TryUse(owner, handle, target, ActiveAbilityUseOrigin.Autonomous)) return;
        }
    }

    /// <summary>辨认普通本人凝神构造，避免打坐时触发额外移动、伤害或世界动作。</summary>
    /// <param name="plan">正式共同编译的实际施放资料。</param>
    private static bool MeditationFocusPlan(CompiledAbility plan)
    {
        if (plan.Use.Channels != ActiveAbilityChannel.World || plan.Use.Target != ActiveAbilityTargetMode.Self ||
            plan.Binding is not { Anchor: AbilityAnchor.Source } || plan.Body != null || plan.HasBarrier ||
            plan.HasWorldEffects || plan.HasWorldMaterials || plan.HasRecovery || plan.HasCorrespondences ||
            plan.FormCount > 16 || plan.Inventory.Length != 0 ||
            plan.Nodes.Any(node => node.Matter.Id == "fire" && node.Matter.State == "energy") ||
            plan.EventOperations.Any(operation => operation.Kind != ConstructionOperationKind.Animate)) return false;
        string stat = BaseStatses.MeditationFocus.id;
        bool any = false;
        foreach (var impact in plan.ActorImpacts)
        {
            if (impact.Targets != SkillUseTargetRelation.Self || impact.HasAgingEffect || impact.HasActorEffectBesidesStatuses ||
                impact.Statuses.Count > 1) return false;
            foreach (var effect in impact.Statuses)
            {
                if (effect.Asset != StatusEffects.CultivationFocus || effect.WaitForDuration || effect.TargetStatRatios?.Count > 0 ||
                    effect.Stats?.Count != 1 || !effect.Stats.TryGetValue(stat, out float bonus) || bonus <= 0f) return false;
                any = true;
            }
        }
        return any;
    }
}
