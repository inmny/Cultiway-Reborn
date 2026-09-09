using System.Collections.Generic;
using Cultiway.Abstract;
using Cultiway.Content.Components;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using UnityEngine;

namespace Cultiway.Content.Systems.Logic;

/// <summary>推进临时命魂人物的维持、牵引、移动、归返和击破结算。</summary>
public sealed class YuanshenSoulCarrierSystem
    : QuerySystem<ActorBinder, YuanshenSoulCarrierState>, IWorldStateClearable
{
    private readonly List<CarrierRequest> completedReturns = new();
    private readonly List<CarrierRequest> brokenCarriers = new();
    private readonly List<Actor> invalidCarriers = new();

    /// <summary>建立只处理存活临时人物的查询。</summary>
    public YuanshenSoulCarrierSystem()
    {
        Filter.WithoutAnyTags(Tags.Get<TagRecycle>());
    }

    /// <summary>世界切换时清空尚未提交的帧内请求。</summary>
    void IWorldStateClearable.ClearWorldState()
    {
        completedReturns.Clear();
        brokenCarriers.Clear();
        invalidCarriers.Clear();
    }

    /// <summary>先只更新状态，再在查询结束后处理归返、击破和销毁。</summary>
    protected override void OnUpdate()
    {
        completedReturns.Clear();
        brokenCarriers.Clear();
        invalidCarriers.Clear();
        float deltaTime = Mathf.Max(0f, Tick.deltaTime);

        Query.ForEachEntity((
            ref ActorBinder binder,
            ref YuanshenSoulCarrierState state,
            Entity _) =>
        {
            Actor carrier = binder.Actor;
            if (!TryResolveOwner(carrier, in state, out ActorExtend owner))
            {
                if (carrier != null) invalidCarriers.Add(carrier);
                return;
            }

            ref YuanshenRuntimeState runtime = ref owner.GetComponent<YuanshenRuntimeState>();
            runtime.travel_elapsed += deltaTime;
            runtime.upkeep_elapsed += deltaTime;
            if (runtime.upkeep_elapsed >= 1f)
            {
                float elapsed = Mathf.Floor(runtime.upkeep_elapsed);
                runtime.upkeep_elapsed -= elapsed;
                if (!YuanshenTravelService.TryPayUpkeep(owner, elapsed))
                    state.action = YuanshenSoulCarrierAction.Returning;
            }

            UpdateTetherCondition(ref state, deltaTime);
            if (!YuanshenTravelService.IsWithinTether(owner, carrier.current_position) &&
                state.tether_condition != YuanshenTetherCondition.Severed)
                state.action = YuanshenSoulCarrierAction.Returning;

            if (state.action == YuanshenSoulCarrierAction.Broken)
            {
                brokenCarriers.Add(new CarrierRequest(owner, carrier));
                return;
            }

            if (state.action == YuanshenSoulCarrierAction.Returning)
            {
                if (state.tether_condition == YuanshenTetherCondition.Severed)
                {
                    if (YuanshenArtifactAnchorService.TryResolve(owner, out _, out Vector3 anchorPosition))
                        state.destination = anchorPosition;
                    else
                        return;
                }
                else
                {
                    state.destination = owner.Base.current_position;
                }
            }

            if (state.action is not (YuanshenSoulCarrierAction.Moving or
                YuanshenSoulCarrierAction.Returning)) return;

            state.movement_refresh_elapsed += deltaTime;
            if (state.movement_refresh_elapsed >= 0.75f || carrier.tile_target == null)
            {
                state.movement_refresh_elapsed = 0f;
                YuanshenTravelService.IssueMove(carrier, state.destination);
            }

            if (Vector2.Distance(carrier.current_position, state.destination) >
                YuanshenTravelService.ReturnCompletionDistance) return;

            if (state.action == YuanshenSoulCarrierAction.Returning)
            {
                if (Vector2.Distance(carrier.current_position, owner.Base.current_position) <=
                    YuanshenTravelService.ReturnCompletionDistance)
                    completedReturns.Add(new CarrierRequest(owner, carrier));
                else
                {
                    state.action = YuanshenSoulCarrierAction.Idle;
                    state.tether_condition = YuanshenTetherCondition.Stable;
                    state.interference_seconds = 0f;
                    carrier.stopMovement();
                }
                return;
            }

            state.action = YuanshenSoulCarrierAction.Idle;
            carrier.stopMovement();
        });

        for (int i = 0; i < completedReturns.Count; i++)
        {
            CarrierRequest request = completedReturns[i];
            YuanshenTravelService.CompleteReturn(request.Owner, request.Carrier);
        }
        for (int i = 0; i < brokenCarriers.Count; i++) ResolveBroken(brokenCarriers[i]);
        for (int i = 0; i < invalidCarriers.Count; i++)
            YuanshenTravelService.RecycleInvalidCarrier(invalidCarriers[i]);
    }

    /// <summary>完整度归零后选择肉身强制归返、法器救援或真正神魂死亡。</summary>
    private static void ResolveBroken(in CarrierRequest request)
    {
        if (!YuanshenTravelService.TryGetSoulCarrier(request.Owner, out Actor current) ||
            current != request.Carrier) return;
        ref YuanshenSoulCarrierState state = ref request.Carrier.GetExtend()
            .GetComponent<YuanshenSoulCarrierState>();
        BaseSimObject attacker = state.last_attacker_actor_id > 0L
            ? World.world?.units?.get(state.last_attacker_actor_id)
            : null;

        if (state.tether_condition == YuanshenTetherCondition.Severed ||
            YuanshenLifecycleService.IsBodiless(request.Owner))
        {
            if (YuanshenLifecycleService.TryRescueBrokenSoulCarrier(request.Owner, request.Carrier)) return;
            YuanshenTravelService.RecycleInvalidCarrier(request.Carrier);
            YuanshenLifecycleService.SubmitTrueSoulDeath(request.Owner, attacker);
            return;
        }

        state.action = YuanshenSoulCarrierAction.Returning;
        state.destination = request.Owner.Base.current_position;
        state.movement_refresh_elapsed = 1f;
        request.Carrier.clearAttackTarget();
        YuanshenTravelService.IssueMove(request.Carrier, state.destination);
    }

    /// <summary>在没有新干扰时缓慢恢复牵引稳定度。</summary>
    private static void UpdateTetherCondition(ref YuanshenSoulCarrierState state, float deltaTime)
    {
        YuanshenTravelService.UpdateTetherCondition(
            ref state.tether_condition,
            ref state.interference_seconds,
            ref state.last_interference_at,
            deltaTime);
    }

    /// <summary>按临时人物组件和所有者总账做双向校验。</summary>
    private static bool TryResolveOwner(
        Actor carrier,
        in YuanshenSoulCarrierState state,
        out ActorExtend owner)
    {
        owner = null;
        if (carrier == null || carrier.isRekt() || !carrier.isAlive()) return false;
        Actor ownerActor = World.world?.units?.get(state.owner_actor_id);
        if (ownerActor == null || ownerActor.isRekt() || !ownerActor.isAlive()) return false;
        owner = ownerActor.GetExtend();
        if (!owner.TryGetComponent(out YuanshenRuntimeState runtime) ||
            runtime.soul_carrier_actor_id != carrier.data.id ||
            runtime.session_id != state.session_id ||
            runtime.soul_carrier_generation != state.generation)
        {
            owner = null;
            return false;
        }
        return true;
    }

    /// <summary>查询结束后提交的一次临时人物生命周期请求。</summary>
    private readonly struct CarrierRequest
    {
        /// <summary>技能与身份所属人物。</summary>
        public readonly ActorExtend Owner;

        /// <summary>临时命魂人物。</summary>
        public readonly Actor Carrier;

        /// <summary>创建一条不可变生命周期请求。</summary>
        public CarrierRequest(ActorExtend owner, Actor carrier)
        {
            Owner = owner;
            Carrier = carrier;
        }
    }
}
