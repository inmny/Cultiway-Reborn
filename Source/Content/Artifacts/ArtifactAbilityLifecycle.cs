using Cultiway.Content.Components;
using Cultiway.Content.Libraries;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3.ActiveAbilities;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Content.Artifacts;

/// <summary>
/// 驱动法器能力从接入、运转到结束的通用运行协议。
/// 能力私有状态仍保存在 values 中，冷却、充能、周期、资源和持续活动统一由此处维护。
/// </summary>
public static partial class ArtifactAbilityLifecycle
{
    /// <summary>按活动运行时快照创建后续帧使用的法器上下文。</summary>
    internal static ArtifactAbilityExecutionContext CreateActivityContext(
        Entity controller,
        Entity artifact,
        ArtifactControlState state,
        in ArtifactAbilityRuntimeEntry runtime)
    {
        return runtime.activity_carrier_actor_id > 0L
            ? new ArtifactAbilityExecutionContext(
                controller,
                artifact,
                state,
                runtime.activity_carrier_actor_id,
                runtime.activity_effect_scale)
            : new ArtifactAbilityExecutionContext(controller, artifact, state);
    }

    /// <summary>把法器上下文转换成后续回调使用的载体上下文。</summary>
    internal static SkillCasterContext ResolveSkillContext(ArtifactAbilityExecutionContext context)
    {
        if (context.controller.IsNull || !context.controller.TryGetComponent(out ActorBinder binder))
            return default;
        ActorExtend owner = binder.AE;
        Actor carrier = context.ResolveCarrierActor();
        ActorExtend carrierExtend = carrier?.GetExtend() ?? owner;
        SkillCasterContext resolved = SkillCasterContextService.Resolve(carrierExtend);
        return new SkillCasterContext(
            owner,
            carrierExtend,
            context.effect_scale,
            resolved.IsValid ? resolved.Kind : SkillCarrierKind.Physical,
            resolved.IsValid && resolved.Kind == SkillCarrierKind.Soul
                ? resolved.HasPhysicalBody
                : true);
    }

    /// <summary>解析持续法器活动当前实际绑定的战斗人物。</summary>
    internal static Actor ResolveActivityCarrier(
        ArtifactAbilityExecutionContext context,
        in ArtifactAbilityRuntimeEntry runtime)
    {
        if (runtime.activity_carrier_actor_id > 0L)
        {
            Actor carrier = World.world?.units?.get(runtime.activity_carrier_actor_id);
            if (carrier != null && !carrier.isRekt()) return carrier;
        }
        return context.ResolveCarrierActor();
    }

    /// <summary>判断持续法器活动是否作用于当前受处理的战斗人物。</summary>
    internal static bool IsActivityCarrier(
        ArtifactAbilityExecutionContext context,
        in ArtifactAbilityRuntimeEntry runtime)
    {
        Actor carrier = ResolveActivityCarrier(context, in runtime);
        Actor current = context.ResolveCarrierActor();
        return carrier != null && current != null && carrier == current;
    }

    /// <summary>
    /// 判断当前控制状态是否达到能力要求的最低层级。
    /// </summary>
    public static bool MeetsState(ArtifactControlState state, ArtifactControlState minimumState)
    {
        return minimumState switch
        {
            ArtifactControlState.Cold => true,
            ArtifactControlState.Ready => state != ArtifactControlState.Cold,
            ArtifactControlState.Operating =>
                state is ArtifactControlState.Operating or ArtifactControlState.Overloaded,
            ArtifactControlState.Overloaded => state == ArtifactControlState.Overloaded,
            _ => false,
        };
    }

    /// <summary>取得法器能力当前剩余冷却秒数。</summary>
    internal static float GetCooldownRemaining(ArtifactAbilityRuntimeEntry runtime)
    {
        return Mathf.Max(0f, (float)(runtime.cooldown_until - Now));
    }

    /// <summary>检查当前法器控制者是否能支付一次主动能力启动消耗。</summary>
    internal static bool CanPayActivationCost(
        ArtifactAbilityAsset asset,
        ArtifactAbilityExecutionContext context,
        ArtifactAbilityInstance ability)
    {
        ArtifactAbilityLifecycleProfile profile = asset.lifecycle;
        float cost = ResolveCost(profile.ResolveActivationCost, profile, context, ability);
        return cost <= 0f || profile.Resource?.Invoke(context, ability, cost, false) == true;
    }

    internal static void EnsureInitialized(
        ArtifactAbilityAsset asset,
        ArtifactAbilityExecutionContext context,
        ArtifactAbilityInstance ability,
        ref ArtifactAbilityRuntimeEntry runtime)
    {
        if (runtime.lifecycle_initialized) return;

        runtime.lifecycle_initialized = true;
        runtime.charges = ResolveMaxCharges(asset.lifecycle, context, ability);
        if (asset.lifecycle.tick_interval > 0f)
        {
            runtime.next_tick_at = Now + asset.lifecycle.tick_interval;
        }
    }

    internal static bool CanHandleEvent(
        ArtifactAbilityAsset asset,
        ArtifactAbilityExecutionContext context,
        ArtifactAbilityInstance ability,
        ArtifactAbilityRuntimeEntry runtime)
    {
        ArtifactAbilityLifecycleProfile profile = asset.lifecycle;
        if (!MeetsState(context.control_state, profile.event_minimum_state)) return false;
        return !profile.event_consumes_trigger || CanTrigger(asset, context, ability, runtime, false);
    }

    internal static bool CanStartActive(
        ArtifactAbilityAsset asset,
        ArtifactAbilityExecutionContext context,
        ArtifactAbilityInstance ability,
        ArtifactAbilityRuntimeEntry runtime)
    {
        ArtifactAbilityLifecycleProfile profile = asset.lifecycle;
        return MeetsState(context.control_state, profile.active_minimum_state) &&
               !HasOtherActiveAbility(context.artifact, ability.instance_id) &&
               CanTrigger(asset, context, ability, runtime, !profile.allow_active_reentry);
    }

    /// <summary>
    /// 判断同一法宝是否正由另一个主要主动能力维持活动。
    /// 被动和事件能力没有主动释放入口，不参与该互斥。
    /// </summary>
    private static bool HasOtherActiveAbility(Entity artifact, string abilityInstanceId)
    {
        ArtifactAbilitySet abilitySet = artifact.GetComponent<ArtifactAbilitySet>();
        ArtifactAbilityRuntime runtime = artifact.GetComponent<ArtifactAbilityRuntime>();
        for (int i = 0; i < abilitySet.abilities.Length; i++)
        {
            ArtifactAbilityInstance otherAbility = abilitySet.abilities[i];
            if (otherAbility.instance_id == abilityInstanceId ||
                runtime.abilities[i].activity_kind == ArtifactAbilityActivityKind.None) continue;

            ArtifactAbilityAsset otherAsset = Libraries.Manager.ArtifactAbilityLibrary.get(otherAbility.ability_id);
            if (otherAsset.active_use != null) return true;
        }
        return false;
    }

    internal static void CommitEvent(
        ArtifactAbilityAsset asset,
        ArtifactAbilityExecutionContext context,
        ArtifactAbilityInstance ability,
        ref ArtifactAbilityRuntimeEntry runtime)
    {
        if (asset.lifecycle.event_consumes_trigger)
        {
            CommitTrigger(
                asset,
                context,
                ability,
                ref runtime,
                runtime.activity_kind != ArtifactAbilityActivityKind.None);
        }
    }

    internal static void CommitActive(
        ArtifactAbilityAsset asset,
        ArtifactAbilityExecutionContext context,
        ArtifactAbilityInstance ability,
        ref ArtifactAbilityRuntimeEntry runtime)
    {
        CommitTrigger(asset, context, ability, ref runtime, true);
    }

    /// <summary>
    /// 将一次短时技能执行绑定到当前能力。执行结束或能力被中断时，生命周期会结束会话并释放其 Body 策略。
    /// </summary>
    public static void BindExecution(ref ArtifactAbilityRuntimeEntry runtime, Entity execution)
    {
        runtime.activity_kind = ArtifactAbilityActivityKind.SkillExecution;
        runtime.active_execution = execution;
        runtime.activity_started_at = Now;
        runtime.activity_until = 0d;
    }

    /// <summary>
    /// 在主动入口或事件处理器中建立一个仅由通用持续时间维护的活动。
    /// 对应能力应通过 ResolveDuration 提供持续时间，成功提交触发后才会开始倒计时。
    /// </summary>
    public static void BeginTimedActivity(ref ArtifactAbilityRuntimeEntry runtime)
    {
        runtime.activity_kind = ArtifactAbilityActivityKind.Timed;
        runtime.activity_started_at = Now;
        runtime.activity_until = 0d;
    }

    /// <summary>建立带权威作用点和朝向的定时活动，供逻辑与视觉共同读取。</summary>
    public static void BeginTimedActivity(
        ref ArtifactAbilityRuntimeEntry runtime,
        Vector3 position,
        Vector3 direction = default)
    {
        BeginTimedActivity(ref runtime);
        runtime.activity_position = position;
        runtime.has_activity_position = true;
        if (direction.sqrMagnitude < 0.0001f) return;
        runtime.activity_direction = direction.normalized;
        runtime.has_activity_direction = true;
    }

    /// <summary>
    /// 主动召回一件法器上指定能力建立的持续活动。
    /// </summary>
    public static bool Recall(Entity artifact, string abilityInstanceId)
    {
        ArtifactAbilitySet abilitySet = artifact.GetComponent<ArtifactAbilitySet>();
        ArtifactAbilityRuntime runtime = artifact.GetComponent<ArtifactAbilityRuntime>();
        for (int i = 0; i < abilitySet.abilities.Length; i++)
        {
            if (abilitySet.abilities[i].instance_id != abilityInstanceId ||
                runtime.abilities[i].activity_kind == ArtifactAbilityActivityKind.None) continue;

            ArtifactAbilityInstance ability = abilitySet.abilities[i];
            ArtifactAbilityAsset asset = Libraries.Manager.ArtifactAbilityLibrary.get(ability.ability_id);
            ArtifactAbilityExecutionContext context = CreateActivityContext(
                runtime.controller,
                artifact,
                runtime.control_state,
                in runtime.abilities[i]);
            EndActivity(asset, context, ability, ref runtime.abilities[i], ArtifactAbilityEndReason.Recalled, true);
            artifact.GetComponent<ArtifactAbilityRuntime>() = runtime;
            return true;
        }
        return false;
    }

    /// <summary>
    /// 驾驭者失去行动能力时立即中断其全部法器活动，避免部署或执行会话滞留到下一轮调度。
    /// </summary>
    public static void InterruptController(Entity controller)
    {
        var relations = controller.GetRelations<EquippedArtifactRelation>();
        for (int i = 0; i < relations.Length; i++)
        {
            Entity artifact = relations[i].artifact;
            ArtifactAbilitySet abilitySet = artifact.GetComponent<ArtifactAbilitySet>();
            ArtifactAbilityRuntime runtime = artifact.GetComponent<ArtifactAbilityRuntime>();
            for (int j = 0; j < abilitySet.abilities.Length; j++)
            {
                if (runtime.abilities[j].activity_kind == ArtifactAbilityActivityKind.None) continue;
                ArtifactAbilityInstance ability = abilitySet.abilities[j];
                ArtifactAbilityAsset asset = Libraries.Manager.ArtifactAbilityLibrary.get(ability.ability_id);
                EndActivity(
                    asset,
                    CreateActivityContext(controller, artifact, relations[i].state, in runtime.abilities[j]),
                    ability,
                    ref runtime.abilities[j],
                    ArtifactAbilityEndReason.ControllerLost,
                    true);
            }
            artifact.GetComponent<ArtifactAbilityRuntime>() = runtime;
        }
    }

    internal static void Synchronize(Entity artifact)
    {
        ArtifactAbilitySet abilitySet = artifact.GetComponent<ArtifactAbilitySet>();
        ArtifactAbilityRuntime runtime = artifact.GetComponent<ArtifactAbilityRuntime>();
        bool hasController = TryResolveController(artifact, out Entity controller, out ArtifactControlState state);

        if (!hasController)
        {
            if (runtime.attached)
            {
                Detach(artifact, abilitySet, ref runtime, ArtifactAbilityEndReason.Unequipped);
                artifact.GetComponent<ArtifactAbilityRuntime>() = runtime;
            }
            return;
        }

        if (!runtime.attached || runtime.controller != controller)
        {
            if (runtime.attached)
            {
                Detach(artifact, abilitySet, ref runtime, ArtifactAbilityEndReason.Replaced);
            }
            Attach(controller, artifact, state, abilitySet, ref runtime);
        }
        else if (runtime.control_state != state)
        {
            ChangeControlState(controller, artifact, state, abilitySet, ref runtime);
        }

        double now = Now;
        for (int i = 0; i < abilitySet.abilities.Length; i++)
        {
            ArtifactAbilityInstance ability = abilitySet.abilities[i];
            ArtifactAbilityAsset asset = Libraries.Manager.ArtifactAbilityLibrary.get(ability.ability_id);
            ArtifactAbilityExecutionContext context = CreateActivityContext(
                controller,
                artifact,
                state,
                in runtime.abilities[i]);
            Advance(asset, context, ability, ref runtime.abilities[i], now);
        }
        artifact.GetComponent<ArtifactAbilityRuntime>() = runtime;
    }

    internal static void ContributeStats(ActorExtend controller, BaseStats stats)
    {
        var relations = controller.E.GetRelations<EquippedArtifactRelation>();
        for (int i = 0; i < relations.Length; i++)
        {
            EquippedArtifactRelation relation = relations[i];
            Entity artifact = relation.artifact;
            ArtifactAbilitySet abilitySet = artifact.GetComponent<ArtifactAbilitySet>();
            ArtifactAbilityRuntime runtime = artifact.GetComponent<ArtifactAbilityRuntime>();
            for (int j = 0; j < abilitySet.abilities.Length; j++)
            {
                ArtifactAbilityInstance ability = abilitySet.abilities[j];
                ArtifactAbilityAsset asset = Libraries.Manager.ArtifactAbilityLibrary.get(ability.ability_id);
                ArtifactAbilityLifecycleProfile profile = asset.lifecycle;
                if (profile.ContributeStats == null ||
                    !MeetsState(relation.state, profile.stats_minimum_state)) continue;
                ArtifactAbilityExecutionContext context = CreateActivityContext(
                    controller.E,
                    artifact,
                    relation.state,
                    in runtime.abilities[j]);
                profile.ContributeStats(context, ability, runtime.abilities[j], stats);
            }
        }
    }
}
