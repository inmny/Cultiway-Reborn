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
    /// <summary>立即生效的双门只占用这一小段出发时间，之后仍走原路径。</summary>
    internal const float TravelPreparation = .35f;

    /// <summary>按实际出行目标寻找能缩短这一段步行的已学本人双门。</summary>
    /// <param name="actor">正在执行出行任务的原人物。</param>
    /// <param name="destination">原任务已经选定的目的地。</param>
    internal static bool TryTravelShortcut(Actor actor, WorldTile destination)
    {
        if (!GeneralSettings.EnableSkillSystems || destination == null || actor.current_tile == null ||
            !destination.isSameIsland(actor.current_tile) || actor.ai.task == null) return false;
        Vector2 goal = destination.posV3, start = actor.current_position;
        Vector2 direction = goal - start;
        if (direction.sqrMagnitude < 16f) return false;
        var task = actor.ai.task;
        bool cityTrip = task == ActorTasks.TravelToCity;
        City city = cityTrip ? destination.zone_city : null;
        long id = actor.id, generation = worldGeneration;
        bool Valid() => generation == worldGeneration && actor.data?.id == id && actor.isAlive() && actor.hasHealth() &&
                        actor.ai.task == task && actor.beh_tile_target == destination && actor.position_height <= 0 &&
                        (!cityTrip || city != null && city.isAlive() && city != actor.city && destination.zone_city == city) &&
                        !actor.isFlying() && !actor.under_forces && !actor.isInMagnet() && !actor.hasTag("frozen_ai") &&
                        !ControllableUnit.isControllingUnit(actor) && !BodyControlState.IsBusy(id) &&
                        actor.attack_target.isRekt() && !CombatWorldService.IsEngaged(actor);
        bool Advances(Actor traveler, Vector3 landing)
        {
            Vector2 point = landing;
            return traveler == actor && Valid() && HasTransferGround(point) &&
                   World.world.GetTileSimple((int)point.x, (int)point.y).isSameIsland(destination) &&
                   Vector2.Distance(actor.current_position, goal) - Vector2.Distance(point, goal) >= 1f;
        }

        if (!Valid()) return false;
        var owner = actor.GetExtend();
        if (!owner.E.TryGetComponent(out ConstructedKnowledge known) || known.Ids.Length == 0) return false;
        Vector3 forward = direction.normalized, point = (Vector3)start + forward;
        var scope = new ConstructionWorldScope(Valid, (_, _) => false, Advances);
        var target = new ActiveAbilityTarget(null, point, worldScope: scope);
        // 不随候选数量反复预估同一条出行，后续出发轮换详细检查的位置。
        int offset = (int)((id + (long)World.world.getCurWorldTime()) % known.Ids.Length);
        var contacts = new List<AbilityCapsule>();
        for (int i = 0; i < Math.Min(8, known.Ids.Length); i++)
        {
            string ability = known.Ids[(offset + i) % known.Ids.Length];
            // 入口需要余力才能产生接触；是否伤人由独立的作用规则决定。
            if (!definitions.TryGetValue(ability, out var definition) || definition.Power <= 0f) continue;
            CompiledAbility plan = PlanFor(ability);
            if (!TravelPlan(plan)) continue;
            var handle = new ActiveAbilityHandle(ProviderId, owner.E, ability);
            if (!ActiveAbilityService.CanUse(owner, handle, target)) continue;

            var ground = GroundProvider?.Invoke(new Rect(start.x - 1f, start.y - 1f, 3f, 3f));
            Vector3 foot = SpatialActorPose.Foot(actor);
            foot.z += ground?.Sample(foot.x, foot.y) ?? 0f;
            Vector3 aim = point;
            aim.z += ground?.Sample(point.x, point.y) ?? 0f;
            float height = SpatialActorPose.Height(actor);
            var preview = new AbilityCast(plan, Matrix4x4.TRS(aim, Quaternion.identity, Vector3.one),
                inputs: new AbilityInputs(foot, aim, forward, height), terrain: ground);
            int step = plan.ActorTransferSteps[0], entry = plan.Steps[step].MappingEntrance;
            if (!preview.TryMapActor(step, entry, id, foot, out Vector3 landing) || !Advances(actor, landing)) continue;
            contacts.Clear();
            preview.AppendContacts(contacts);
            var body = ActorHitBoundary.Create(foot, height, Vector3.zero);
            if (!contacts.Any(form => form.Node == entry && AbilityPairSweep.Find(form, form, body, body, out _, out _, out _)))
                continue;
            if (ActiveAbilityService.TryUse(owner, handle, target, ActiveAbilityUseOrigin.Autonomous)) return true;
        }
        return false;
    }

    /// <summary>这段出行只选择立即可接触、只搬自己的双门；混合用途仍可手动施放。</summary>
    /// <param name="plan">正式登记的构造计划。</param>
    private static bool TravelPlan(CompiledAbility plan)
    {
        if (plan.Use.Channels != ActiveAbilityChannel.World ||
            plan.Use.Target is not (ActiveAbilityTargetMode.Point or ActiveAbilityTargetMode.ObjectOrPoint) ||
            plan.Binding is not { Anchor: AbilityAnchor.Source, Follow: false } ||
            plan.Body != null || plan.HasBarrier || plan.HasWorldEffects || plan.HasWorldMaterials || plan.HasRecovery ||
            plan.HasCorrespondences || plan.ActorTransferSteps.Length != 1 || plan.FormCount > 16 ||
            plan.Nodes.Any(node => node.Matter.Id == "fire" && node.Matter.State == "energy") ||
            plan.EventOperations.Any(operation => operation.Kind is not (ConstructionOperationKind.Animate or
                ConstructionOperationKind.Detach or ConstructionOperationKind.MapActor or ConstructionOperationKind.Ready))) return false;
        var move = plan.Steps[plan.ActorTransferSteps[0]];
        if (move.Action.At > 0f || plan.Nodes[move.Node].Presence.x > 0f || plan.Nodes[move.MappingExit].Presence.x > 0f ||
            plan.ContactPolicies[plan.Nodes[move.Node].ContactGroup].Mode == ConstructionContactMode.Pulse ||
            ((ValueTuple<float, int?, float>)move.Action.Parameters).Item3 > 0f) return false;
        return plan.ActorImpacts.All(impact => impact.Targets == SkillUseTargetRelation.Self &&
                                               !impact.SelectedTargetOnly && !impact.HasActorEffect);
    }
}
