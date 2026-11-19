using Cultiway.Const;
using Cultiway.Content.KnightCombat;
using Cultiway.Core;
using Cultiway.Core.Combat.Tactical;
using Cultiway.Core.SkillLibV3.ActiveAbilities;
using Cultiway.Core.Skills;
using UnityEngine;

namespace Cultiway.Content.Skills;

/// <summary>在原训练任务中选择已学身体动作，沿共同动作时间和实际支付练习。</summary>
public sealed partial class ConstructedAbilityService
{
    /// <summary>工作选择和主控命令只给真正有可练动作的人提供训练入口。</summary>
    /// <param name="owner">可能参加训练的人物。</param>
    internal static bool HasPracticeConstruction(ActorExtend owner)
    {
        if (!GeneralSettings.EnableSkillSystems || !owner.E.TryGetComponent(out ConstructedKnowledge known)) return false;
        foreach (string id in known.Ids)
            if (TryPracticePlan(id, out var plan) && BodyActionBinding.CanBind(owner, plan.Body)) return true;
        return false;
    }

    /// <summary>到达实际假人后，从已学动作选一次可承担的练习；原知识和目标选择规则不变。</summary>
    /// <param name="owner">执行原训练任务的实际人物。</param>
    /// <param name="seconds">实际动作正常完成所需的世界秒数，失败时为零。</param>
    internal static bool TryPracticeConstruction(ActorExtend owner, out float seconds)
    {
        seconds = 0f;
        if (!GeneralSettings.EnableSkillSystems || !owner.E.TryGetComponent(out ConstructedKnowledge known) || known.Ids.Length == 0)
            return false;
        Actor actor = owner.Base;
        Building dummy = actor.beh_building_target;
        if (!KnightTrainingDummyService.IsUsable(actor, dummy)) return false;
        long id = actor.id, dummyId = dummy.getID(), generation = worldGeneration;
        bool Valid() => generation == worldGeneration && actor.data?.id == id && actor.isAlive() && actor.hasHealth() &&
                        actor.ai.task == ActorTasks.DailyKnightTrain && actor.beh_building_target == dummy &&
                        KnightTrainingDummyService.IsUsable(actor, dummy) && dummy.getID() == dummyId &&
                        actor.attack_target.isRekt() && !CombatWorldService.IsEngaged(actor) && !actor.isInMagnet();
        if (!Valid()) return false;

        var target = new ActiveAbilityTarget(null, (Vector2)dummy.current_position,
            worldScope: new ConstructionWorldScope(Valid, (_, _) => false));
        int offset = (int)((id + (long)LogicTime) % known.Ids.Length);
        for (int index = 0; index < known.Ids.Length; index++)
        {
            string ability = known.Ids[(offset + index) % known.Ids.Length];
            if (!TryPracticePlan(ability, out var plan)) continue;
            var handle = new ActiveAbilityHandle(ProviderId, owner.E, ability);
            if (!simulation.ResolveControlState(owner, handle).CanUse ||
                !Start(owner, plan, target, ActiveAbilityUseOrigin.Practice, out _, practice: true)) continue;
            seconds = Mathf.Max(plan.Lifetime, plan.Body.Duration);
            return true;
        }
        return false;
    }

    /// <summary>一人即可练习的近身动作；涉及其他人物、材料或世界操作的整招继续留给正式施放。</summary>
    /// <param name="id">已经掌握的实际版本。</param>
    /// <param name="plan">对应的共用计划。</param>
    private static bool TryPracticePlan(string id, out CompiledAbility plan)
    {
        plan = null;
        if (!definitions.TryGetValue(id, out var definition) || !ConstructionResources.Same(definition.Resource, "stamina"))
            return false;
        plan = PlanFor(id);
        return plan.Body != null && plan.FormCount == 0 && plan.Binding == null && plan.Inventory.Length == 0 &&
               plan.ActorTransferSteps.Length == 0 && plan.EventOperations.Length == 0 && !plan.HasWorldEffects &&
               !plan.HasWorldMaterials && !plan.Body.Construction.HasControlRelations &&
               (plan.Use.Channels & ActiveAbilityChannel.Combat) != 0;
    }
}
