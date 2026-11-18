using System;
using System.Collections.Generic;
using Cultiway.Core.Combat;
using Cultiway.Core.EventSystem.Events;
using Cultiway.Core.Skills;
using Cultiway.Core.SkillLibV3.Effects;
using Cultiway.Core.SkillLibV3.Usage;
using UnityEngine;

namespace Cultiway.Content.Skills;

/// <summary>实际反射及部位归属的主线程交接，不创建额外施放或再次支付攻击资源。</summary>
public sealed partial class ConstructedAbilityService
{
    /// <summary>反射已经改变运动、归属和屏障余量后发布。</summary>
    public static event Action<ConstructedReflection> ReflectionConfirmed;

    /// <summary>当前控制者的稳定身份及已经冻结的伤害归属。</summary>
    private sealed class CastAuthority
    {
        /// <summary>读取原版已维护的个人敌意，只在主线程采集稳定编号。</summary>
        private static readonly HarmonyLib.AccessTools.FieldRef<Actor, HashSet<long>> Aggression =
            HarmonyLib.AccessTools.FieldRefAccess<Actor, HashSet<long>>("_aggression_targets");

        /// <summary>最后有效时的个人敌意，不保留池中人物引用。</summary>
        private readonly HashSet<long> hostileIds = new();

        /// <summary>最后有效的阵营编号。</summary>
        private long kingdomId;

        /// <summary>本次明确攻击阵营不随归属人物转属而变化。</summary>
        private readonly bool fixedKingdom;

        /// <summary>本次空间载体也属于自身，不能在出生处被自己的攻击命中。</summary>
        private readonly long spatialId;

        /// <summary>最后采集关系的真实帧，同帧重复校验不重新复制。</summary>
        private int relationsFrame = -1;

        /// <summary>最后有效身体是否参与战斗，以及是否处于公开发狂敌对。</summary>
        private bool canFight, tantrum;

        /// <summary>最后有效的来源脚下；失效后施力中心不读取尸体搬运或对象池新位置。</summary>
        internal Vector3 LastFoot;

        /// <summary>实际控制者，只在主线程检查。</summary>
        internal readonly Actor Actor;

        /// <summary>对象池重用时仍可识别身份。</summary>
        internal readonly long Id;

        /// <summary>已经确定的归属及能级，交锋后不重复缩放。</summary>
        internal readonly GetHitEvent Damage;

        /// <summary>追踪的上一控制者及其稳定编号。</summary>
        internal readonly Actor Target;

        /// <summary>追踪目标的稳定编号。</summary>
        internal readonly long TargetId;

        /// <summary>固定环境来源不拥有或借用人物。</summary>
        internal bool WorldSource => Actor == null;

        /// <summary>当前人物来源是否仍有效；环境的存续由发起过程负责。</summary>
        internal bool IsLive => Actor != null && Actor.data?.id == Id && Actor.isAlive() && Actor.hasHealth();

        /// <summary>介质只能借给同一实际阵营，来源失效后使用已经冻结的阵营身份。</summary>
        /// <param name="other">另一实际控制者。</param>
        internal bool SameKingdom(CastAuthority other) => kingdomId != 0 && kingdomId == other.kingdomId;

        /// <summary>保留当前实际控制者与已冻结模板。</summary>
        /// <param name="actor">当前控制者。</param>
        /// <param name="damage">当前伤害归属。</param>
        /// <param name="target">追踪目标。</param><param name="attackKingdom">可选的本次明确攻击阵营。</param>
        /// <param name="spatialSourceId">本次空间载体的稳定身份，省略时沿用归属人物。</param>
        internal CastAuthority(Actor actor, GetHitEvent damage, Actor target, Kingdom attackKingdom = null, long spatialSourceId = 0)
        {
            Actor = actor;
            Id = actor?.data.id ?? 0;
            spatialId = spatialSourceId == 0 ? Id : spatialSourceId;
            Damage = damage;
            Target = target;
            TargetId = target?.data?.id ?? 0;
            fixedKingdom = attackKingdom != null;
            kingdomId = attackKingdom?.data?.id ?? 0;
            RefreshRelations();
        }

        /// <summary>反射复制当前控制者身份；失效后也不能借用其对象池新身份。</summary>
        /// <param name="controller">屏障真实控制者。</param><param name="damage">已交接的伤害。</param>
        /// <param name="target">上一控制者。</param>
        internal CastAuthority(CastAuthority controller, GetHitEvent damage, CastAuthority target)
        {
            Actor = controller.Actor;
            Id = controller.Id;
            Damage = damage;
            Target = target.Actor;
            TargetId = target.Id;
            kingdomId = controller.kingdomId;
            fixedKingdom = controller.fixedKingdom;
            spatialId = controller.spatialId;
            canFight = controller.canFight;
            tantrum = controller.tantrum;
            LastFoot = controller.LastFoot;
            hostileIds.UnionWith(controller.hostileIds);
            relationsFrame = controller.relationsFrame;
        }

        /// <summary>只改变后续部位的目标，保留原稳定归属和已经采集的关系。</summary>
        /// <param name="source">真实来源部位的控制者。</param><param name="target">已在主线程选中的人物。</param>
        internal CastAuthority(CastAuthority source, Actor target)
        {
            Actor = source.Actor; Id = source.Id; Damage = source.Damage;
            Target = target; TargetId = target.data.id;
            kingdomId = source.kingdomId; fixedKingdom = source.fixedKingdom; spatialId = source.spatialId; canFight = source.canFight; tantrum = source.tantrum;
            LastFoot = source.LastFoot; relationsFrame = source.relationsFrame;
            hostileIds.UnionWith(source.hostileIds);
        }

        /// <summary>活着时保留最后有效关系；失效或对象池换人后不再读取该对象。</summary>
        internal void RefreshRelations()
        {
            if (!IsLive || relationsFrame == Time.frameCount) return;
            relationsFrame = Time.frameCount;
            if (!fixedKingdom) kingdomId = Actor.kingdom?.data?.id ?? 0;
            LastFoot = PhysicsFoot(Actor);
            canFight = !Actor.asset.skip_fight_logic;
            tantrum = Actor.hasStatusTantrum();
            hostileIds.Clear();
            hostileIds.UnionWith(Aggression(Actor));
        }

        /// <summary>人物有效时读取真实关系；独立存续时只使用最后有效稳定资料。</summary>
        /// <param name="relation">真实作用所需关系。</param><param name="target">当前有效目标。</param>
        internal bool Matches(SkillUseTargetRelation relation, Actor target)
        {
            if (WorldSource) return relation == SkillUseTargetRelation.Hostile && target.id == TargetId &&
                target.asset.can_be_killed_by_stuff && !target.isInsideSomething() &&
                target.ai.action?.special_prevent_can_be_attacked != true && !target.isInMagnet();
            if (target.data.id == spatialId) return relation is SkillUseTargetRelation.Friendly or SkillUseTargetRelation.Self;
            if (IsLive) return SkillTargetRelationResolver.Matches(relation, Actor, target,
                fixedKingdom ? World.world.kingdoms.getCivOrWildViaID(kingdomId) : null);
            if (target.data.id == Id) return relation != SkillUseTargetRelation.Hostile;
            // 原版野外阵营使用负编号，与文明国家分库存放。
            Kingdom kingdom = World.world.kingdoms.getCivOrWildViaID(kingdomId);
            bool hostile = kingdom?.isEnemy(target.kingdom) == true || target.hasStatusTantrum() ||
                           hostileIds.Contains(target.data.id);
            return relation switch
            {
                SkillUseTargetRelation.Hostile => canFight && hostile && target.asset.can_be_killed_by_stuff &&
                                                  !target.isInsideSomething() &&
                                                  target.ai.action?.special_prevent_can_be_attacked != true &&
                                                  !target.isInMagnet(),
                SkillUseTargetRelation.Friendly => !hostile && kingdom != null && target.kingdom != null &&
                                                   (kingdom == target.kingdom || kingdom.getAlliance() != null &&
                                                       kingdom.getAlliance() == target.kingdom.getAlliance()),
                _ => false
            };
        }

        /// <summary>双方独立存续时也按真实保存的阵营和个人敌意参与交锋。</summary>
        /// <param name="other">来袭时确定的阵营。</param><param name="actorId">来袭人物的稳定编号，非人物为零。</param>
        internal bool IsHostileTo(Kingdom other, long actorId) => WorldSource ? actorId != 0 && actorId == TargetId : Id != actorId && spatialId != actorId &&
            (actorId > 0 && hostileIds.Contains(actorId) || other != null &&
            World.world.kingdoms.getCivOrWildViaID(kingdomId)?.isEnemy(other) == true);

        /// <summary>双方独立存续时仍保留真实敌我关系。</summary>
        /// <param name="other">另一施放的稳定来源。</param>
        internal bool IsHostileTo(CastAuthority other)
        {
            if (WorldSource) return other.Id != 0 && other.Id == TargetId;
            if (other.WorldSource) return Id != 0 && Id == other.TargetId;
            if (!other.fixedKingdom && other.IsLive) return Matches(SkillUseTargetRelation.Hostile, other.Actor);
            return Id != other.Id && spatialId != other.Id && canFight && (hostileIds.Contains(other.Id) || other.tantrum ||
                                                  World.world.kingdoms.getCivOrWildViaID(kingdomId)
                                                      ?.isEnemy(World.world.kingdoms.getCivOrWildViaID(other.kingdomId)) == true);
        }
    }

    /// <summary>容量足够且来袭部位真正独立运动时执行反射，否则保留正常吸收。</summary>
    /// <param name="hit">共同时间最早的接触。</param>
    private static bool TryReflect(in ContactChoice hit)
    {
        ShapeMotion left = hit.Left, right = hit.Right.Value;
        bool leftWard = left.Running.Cast.Role(left.To.Node) == ConstructionContactRole.Barrier;
        ShapeMotion ward = leftWard ? left : right, attack = leftWard ? right : left;
        int guardNode = ward.To.Node, node = attack.To.Node;
        if (ward.Running.Cast.Role(guardNode) != ConstructionContactRole.Barrier ||
            ward.Running.Cast.Plan.Nodes[guardNode].Interception != ConstructionInterception.Reflect ||
            !attack.Running.Cast.CanReflect(node)) return false;
        float force = attack.Running.Cast.RemainingForce(node);
        float barrierBefore = ward.Running.Cast.RemainingForce(guardNode);
        if (barrierBefore < force) return false;
        Vector3 normal = leftWard ? hit.Normal : -hit.Normal;
        if (normal.sqrMagnitude < .5f) return false;
        Vector3 surface = ward.SurfaceVelocity(hit.Point, normal, hit.Fraction, hit.Seconds);
        Vector3 incoming = attack.Running.Cast.Velocity(node);
        if (Vector3.Dot(incoming - surface, normal) >= -.00001f) return false;
        CastAuthority previous = attack.Running.Owner(node), defender = ward.Running.Owner(guardNode);
        var damage = previous.Damage;
        damage.AttackerID = defender.Damage.AttackerID;
        damage.AttackerIsActor = defender.Damage.AttackerIsActor;
        damage.HasAttacker = defender.Damage.HasAttacker;
        damage.DamageOrigin = DamageOrigin.Reaction;
        var owner = new CastAuthority(defender, damage, previous);
        Vector3 aim = previous.IsLive
            ? Combat.SpatialActorPose.Foot(previous.Actor) +
              Vector3.forward * Combat.SpatialActorPose.Height(previous.Actor) * .5f
            : attack.Running.LastTarget;
        attack.Running.Cast.Reflect(node, normal, surface, aim);
        attack.Running.Owners[node] = owner;
        for (int i = node + 1; i < attack.Running.Owners.Length; i++)
            if (attack.Running.Cast.IsAttachedBelow(i, node))
                attack.Running.Owners[i] = owner;
        attack.Running.HasTransfers = true;
        attack.Running.Cast.ConfirmClash(node, hit.Point, 0f);
        ward.Running.Cast.ConfirmClash(guardNode, hit.Point, force);
        ward.Running.Cast.ConsumeForce(guardNode, force);
        float barrierAfter = ward.Running.Cast.RemainingForce(guardNode);
        ReflectionConfirmed?.Invoke(new ConstructedReflection(attack.Running.Id, attack.Running.Cast.Plan.Id,
            node, attack.Running.SourceId, previous.Id, owner.Id, attack.Running.Cast.Reflections(node),
            LogicTime, attack.Running.Cast.Age, hit.Point, normal, incoming, attack.Running.Cast.Velocity(node),
            force, ward.Running.Id, guardNode, barrierAfter, ward.Running.SourceId, defender.Id,
            barrierBefore - barrierAfter, ContactPartPresent(ward.Running, guardNode)));
        return true;
    }
}