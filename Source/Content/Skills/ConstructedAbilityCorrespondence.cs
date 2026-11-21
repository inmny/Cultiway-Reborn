using System;
using Cultiway.Content.KnightCombat;
using Cultiway.Core.Combat;
using Cultiway.Core.EventSystem.Systems;
using Cultiway.Core.Skills;
using Cultiway.Utils.Extension;
using UnityEngine;

namespace Cultiway.Content.Skills;

/// <summary>真实映像受击后传到原人物的伤害事实，不冒充直接身体接触。</summary>
public readonly struct ConstructedCorrespondenceDamage
{
    /// <summary>攻击施放、映像施放、实际攻击者和原受害人物。</summary>
    public readonly long AttackCastId, ImageCastId, OwnerId, TargetId;

    /// <summary>双方实际部位。</summary>
    public readonly int AttackNode, ImageNode;

    /// <summary>所属映像部位内的明确对应名称。</summary>
    public readonly string Relation;

    /// <summary>实际交锋点及共同世界时刻。</summary>
    public readonly Vector3 Position;

    /// <summary>真实结算时刻。</summary>
    public readonly double WorldTime;

    /// <summary>真实消耗、传递份额、按交锋量折算的基础伤害和实际生命前后值。</summary>
    public readonly float ConsumedForce, Fraction, Damage, HealthBefore, HealthAfter;

    /// <summary>原人物实际生命扣除与护盾吸收。</summary>
    public readonly DamageSettlement Settlement;

    /// <summary>原人物已经结算后建立事实。</summary>
    /// <param name="attack">实际攻击施放。</param><param name="image">实际映像施放。</param>
    /// <param name="owner">伤害归属。</param><param name="target">原受害人物。</param>
    /// <param name="attackNode">攻击部位。</param><param name="imageNode">映像部位。</param>
    /// <param name="relation">明确关系。</param><param name="position">真实交锋点。</param>
    /// <param name="time">共同世界时刻。</param><param name="consumed">实际消耗。</param>
    /// <param name="fraction">声明份额。</param><param name="damage">按交锋量折算的基础伤害。</param>
    /// <param name="before">原生命值。</param><param name="after">结果生命值。</param>
    /// <param name="settlement">原人物的实际受击结果。</param>
    internal ConstructedCorrespondenceDamage(long attack, long image, long owner, long target, int attackNode,
        int imageNode, string relation, Vector3 position, double time, float consumed, float fraction,
        float damage, float before, float after, DamageSettlement settlement)
    {
        AttackCastId = attack;
        ImageCastId = image;
        OwnerId = owner;
        TargetId = target;
        AttackNode = attackNode;
        ImageNode = imageNode;
        Relation = relation;
        Position = position;
        WorldTime = time;
        ConsumedForce = consumed;
        Fraction = fraction;
        Damage = damage;
        HealthBefore = before;
        HealthAfter = after;
        Settlement = settlement;
    }
}

public sealed partial class ConstructedAbilityService
{
    /// <summary>真实映像交锋已沿原伤害入口作用原人物后通知。</summary>
    public static event Action<ConstructedCorrespondenceDamage> CorrespondenceDamageConfirmed;

    /// <summary>取得受理时的原端点，不跟随后来反射的选敌。</summary>
    /// <param name="running">实际映像施放。</param><param name="node">实际映像部位。</param>
    private static Actor CorrespondenceActor(RunningCast running, int node)
    {
        var relation = running.Cast.Plan.Nodes[node].Correspondence;
        return relation.Target == ConstructionCorrespondenceTarget.Source ? running.Source : running.Target;
    }

    /// <summary>原端点失效即终止该联系，对象池重用或复活不能重新接续。</summary>
    /// <param name="running">当前正式施放。</param>
    private static void ValidateCorrespondences(RunningCast running)
    {
        if (running.CorrespondenceEnded == null) return;
        for (int node = 0; node < running.CorrespondenceEnded.Length; node++)
        {
            var relation = running.Cast.Plan.Nodes[node].Correspondence;
            if (relation == null || running.CorrespondenceEnded[node]) continue;
            Actor actor = CorrespondenceActor(running, node);
            long id = relation.Target == ConstructionCorrespondenceTarget.Source ? running.SourceId : running.TargetId;
            if (actor == null || actor.data?.id != id || !actor.isAlive() || !actor.hasHealth())
                running.CorrespondenceEnded[node] = true;
        }
    }

    /// <summary>读取当前真实对应，预测筛选不改写正式记录。</summary>
    /// <param name="running">映像施放。</param><param name="node">实际部位。</param>
    /// <param name="current">确认时核对当前时段，预测时由共同区间另行确认。</param>
    private static Actor CorrespondenceReceiver(RunningCast running, int node, bool current = true)
    {
        if (node < 0 || running.CorrespondenceEnded == null || running.CorrespondenceEnded[node] ||
            running.Cast.Plan.Nodes[node].Correspondence == null ||
            current && !running.Cast.CorrespondenceActive(node) ||
            !running.Cast.IsNodePresent(node) && running.Cast.ContactSweepForce(node) <= 0f) return null;
        Actor actor = CorrespondenceActor(running, node);
        var relation = running.Cast.Plan.Nodes[node].Correspondence;
        long id = relation.Target == ConstructionCorrespondenceTarget.Source ? running.SourceId : running.TargetId;
        return actor != null && actor.data?.id == id && actor.isAlive() && actor.hasHealth() ? actor : null;
    }

    /// <summary>映像的可受击关系读取原人物，不以映像制作人的阵营挡住合法攻击。</summary>
    /// <param name="attack">真实攻击形状。</param><param name="image">实际映像形状。</param>
    private static bool CorrespondenceAllows(in ShapeMotion attack, in ShapeMotion image)
    {
        Actor receiver = CorrespondenceReceiver(image.Running, image.To.Node);
        return receiver != null && IsMirrorInitiator(attack, image) && CorrespondenceAllows(attack.Running, attack.To.Node,
            attack.Running.Cast.Impact(attack.To.Node), image.Running.Cast.Plan.Nodes[image.To.Node].Correspondence, receiver);
    }

    /// <summary>同阵营形体只在原人物可接收的真实窗口内引发接触。</summary>
    /// <param name="attack">实际来袭面。</param><param name="image">实际映像面。</param>
    /// <param name="from">允许接触的最早进度。</param><param name="until">停止传递的进度。</param>
    private static bool CorrespondenceRange(in ShapeMotion attack, in ShapeMotion image, out float from, out float until)
    {
        from = float.PositiveInfinity;
        until = 0f;
        Actor receiver = CorrespondenceReceiver(image.Running, image.To.Node, current: false);
        return receiver != null && IsMirrorInitiator(attack, image) &&
               CorrespondenceAllows(attack.Running, attack.To.Node, attack.Running.Cast.Impact(attack.To.Node),
                   image.Running.Cast.Plan.Nodes[image.To.Node].Correspondence, receiver) &&
               image.Running.Cast.CorrespondenceInterval(image.To.Node, out from, out until);
    }

    /// <summary>原人物必须符合来袭部位的对象限制，双方至少同意一种实际作用。</summary>
    /// <param name="running">来袭施放。</param><param name="node">来袭部位。</param>
    /// <param name="impact">该作用阶段的资料。</param><param name="relation">映像许可。</param>
    /// <param name="receiver">对应的原人物。</param>
    private static bool CorrespondenceAllows(RunningCast running, int node, CompiledConstructionImpact impact,
        CompiledCorrespondence relation, Actor receiver)
    {
        CastAuthority owner = running.Owner(node);
        return (!impact.SelectedTargetOnly || owner.TargetId == receiver.id) && owner.Matches(impact.Targets, receiver) &&
               (relation.DamageFraction > 0f && impact.DamageScale > 0f || CorrespondenceEffects(impact, relation) != 0);
    }

    /// <summary>只有映像许可且来袭部位实际拥有的作用才可转交。</summary>
    /// <param name="impact">来袭部位的作用。</param><param name="relation">映像许可。</param>
    private static ConstructionCorrespondenceEffects CorrespondenceEffects(CompiledConstructionImpact impact,
        CompiledCorrespondence relation)
    {
        var effects = ConstructionCorrespondenceEffects.None;
        if (impact.Restoration?.Target == ConstructionRestorationTarget.Contact && impact.Restoration.HasEffect)
            effects |= ConstructionCorrespondenceEffects.Restoration;
        if (impact.Statuses.Count > 0 || impact.StaggerSeconds > 0 || impact.BindSeconds > 0 ||
            impact.StrainSeconds > 0 || impact.ExposureSeconds > 0)
            effects |= ConstructionCorrespondenceEffects.Status;
        if (impact.Force != 0f || impact.Launch || impact.Impulse?.sqrMagnitude > 0f || impact.Knockback?.HasEffect == true)
            effects |= ConstructionCorrespondenceEffects.Force;
        return effects & relation.Effects;
    }

    /// <summary>映像与直接人物接触共用次数和时点，传递不会再递归触发另一份命中。</summary>
    /// <param name="running">真实攻击施放。</param><param name="node">攻击部位。</param>
    /// <param name="target">原人物编号。</param>
    private static bool CorrespondenceContactReady(RunningCast running, int node, long target)
    {
        var key = running.HitKey(node, target);
        var policy = running.Cast.Plan.ContactPolicies[key.Item1];
        running.Hits.TryGetValue(key, out ContactMemory memory);
        running.GroupHits.TryGetValue(key.Item1, out int count);
        if (policy.MaximumTotal > 0 && count >= policy.MaximumTotal ||
            policy.MaximumPerTarget > 0 && memory?.Count >= policy.MaximumPerTarget) return false;
        return policy.Mode switch
        {
            ConstructionContactMode.Once => memory == null,
            ConstructionContactMode.Interval => memory == null || running.Cast.Age >= memory.Age + policy.Interval,
            ConstructionContactMode.Pulse => PulseFraction(policy, memory, running.Cast.Age - 1f, 1f, 1f) <= 1f,
            ConstructionContactMode.Reentry => memory?.Touching != true,
            _ => false
        };
    }

    /// <summary>实际消耗后领取一次共同接触机会，按顺序向原人物提交获准的伤害和附带作用。</summary>
    /// <param name="attack">实际攻击。</param><param name="image">实际映像。</param>
    /// <param name="receiver">消耗前确认的原人物，允许本次交锋打碎映像后完成最后一次传递。</param>
    /// <param name="consumed">已经实际扣除的作用量。</param><param name="point">共同交锋点。</param>
    /// <param name="contactConfirmed">身体已按完整共同表面及频率确认资格。</param>
    private static ApplyAttackState ApplyCorrespondence(in ShapeMotion attack, in ShapeMotion image, Actor receiver,
        float consumed, Vector3 point, bool contactConfirmed = false)
    {
        if (receiver == null || !receiver.isAlive() || !receiver.hasHealth()) return ApplyAttackState.Continue;
        var relation = image.Running.Cast.Plan.Nodes[image.To.Node].Correspondence;
        long id = relation.Target == ConstructionCorrespondenceTarget.Source ? image.Running.SourceId : image.Running.TargetId;
        var impact = attack.Running.Cast.Impact(attack.To.Node);
        CastAuthority owner = attack.Running.Owner(attack.To.Node);
        float amount = consumed * relation.DamageFraction * impact.DamageScale;
        var effects = CorrespondenceEffects(impact, relation);
        if (receiver.data?.id != id || consumed <= 0f || amount <= 0f && effects == 0 ||
            !CorrespondenceAllows(attack.Running, attack.To.Node, impact, relation, receiver) ||
            !contactConfirmed && !CorrespondenceContactReady(attack.Running, attack.To.Node, id)) return ApplyAttackState.Continue;
        if (relation.MirrorImage?.Fracture != null)
            return BeginMirrorCrack(attack, image, receiver, relation, consumed, point);
        RememberContact(attack.Running, attack.To.Node, id);
        float before = receiver.data.health;
        long generation = worldGeneration;
        ApplyAttackState result = ApplyAttackState.Hit;
        DamageSettlement settlement = default;
        Vector3? priorVelocity = (effects & ConstructionCorrespondenceEffects.Force) != 0 && impact.Impulse.HasValue
            ? ConstructedActorImpulse.Velocity(receiver) : null;
        if (amount > 0f && attack.To.Node < 0)
        {
            float multiplier = attack.Running.Power * impact.DamageScale * relation.DamageFraction *
                               consumed / BodyCorrespondenceForce(attack.Running);
            var caster = attack.Running.Source.GetExtend();
            var observation = new DamageObservation(caster.Base.id, id);
            result = attack.Running.Body.Weapon == null
                ? KnightWeaponStrikeResolver.StrikeUnarmed(caster, receiver, null, multiplier, immediate: true,
                    observer: observation, damageOrigin: DamageOrigin.Reaction).state
                : KnightWeaponStrikeResolver.StrikeBorrowed(caster, attack.Running.Body.Weapon, receiver, multiplier,
                    immediate: true, observer: observation, damageOrigin: DamageOrigin.Reaction).state;
            settlement = observation.Result;
        }
        else if (amount > 0f)
        {
            var damage = owner.Damage;
            damage.DamageOrigin = DamageOrigin.Reaction;
            damage.TargetID = id;
            damage.Damage = amount;
            damage.Element = attack.Running.Cast.FixedDamageElement(attack.To.Node, impact) ??
                Composition(attack.Running.Cast.Matter(attack.To.Node).DamageKind);
            settlement = GetHitEventSystem.Resolve(damage);
        }
        if (generation != worldGeneration) return result;
        float after = receiver.data?.id == id ? receiver.data.health : 0f;
        if (effects != 0 && receiver.data?.id == id && receiver.isAlive() && receiver.hasHealth())
            ApplyCorrespondenceEffects(attack, image, receiver, impact, effects, result, consumed, point, priorVelocity);
        if (generation != worldGeneration) return result;
        EndExhaustedContactGroup(attack.Running, attack.To.Node);
        if (amount > 0f)
            CorrespondenceDamageConfirmed?.Invoke(new ConstructedCorrespondenceDamage(attack.Running.Id,
                image.Running.Id, owner.Id, id, attack.To.Node, image.To.Node, relation.Id, point,
                simulation.contactTime, consumed, relation.DamageFraction, amount, before, after, settlement));
        return result;
    }
}
