using System;
using Cultiway.Core.Skills;
using UnityEngine;

namespace Cultiway.Content.Skills;

/// <summary>一次实际映像接触获准提交的附带作用及原人物的即时变化。</summary>
public readonly struct ConstructedCorrespondenceEffects
{
    /// <summary>来袭施放、映像施放、实际来源与原人物。</summary>
    public readonly long AttackCastId, ImageCastId, OwnerId, TargetId;

    /// <summary>映像部位内的关系名称和结算时点。</summary>
    public readonly string Relation;

    /// <summary>此次结算的共同世界时刻。</summary>
    public readonly double WorldTime;

    /// <summary>已提交的作用类别，实际状态仍遵守原人物抵抗和替换规则。</summary>
    public readonly ConstructionCorrespondenceEffects Effects;

    /// <summary>实际交锋消耗与本次即时恢复量。</summary>
    public readonly float ConsumedForce, HealthRestored;

    /// <summary>明确受力是否实际受理，以及实际速度变化。</summary>
    public readonly bool ForceApplied;

    /// <summary>明确受力造成的速度变化。</summary>
    public readonly Vector3 VelocityChange;

    /// <summary>实际接触发生并对原人物结算后的记录。</summary>
    /// <param name="attack">来袭施放。</param><param name="image">映像施放。</param>
    /// <param name="owner">实际归属。</param><param name="target">对应的原人物。</param>
    /// <param name="relation">真实关系名称。</param><param name="time">共同世界时刻。</param>
    /// <param name="effects">提交的明确作用。</param><param name="consumed">实际交锋消耗。</param>
    /// <param name="restored">即时恢复的生命值。</param><param name="forceApplied">受力是否受理。</param>
    /// <param name="velocity">明确受力造成的速度变化。</param>
    internal ConstructedCorrespondenceEffects(long attack, long image, long owner, long target, string relation,
        double time, ConstructionCorrespondenceEffects effects, float consumed, float restored, bool forceApplied,
        Vector3 velocity)
    {
        AttackCastId = attack;
        ImageCastId = image;
        OwnerId = owner;
        TargetId = target;
        Relation = relation;
        WorldTime = time;
        Effects = effects;
        ConsumedForce = consumed;
        HealthRestored = restored;
        ForceApplied = forceApplied;
        VelocityChange = velocity;
    }
}

/// <summary>只将映像允许的明确作用提交给实际原人物。</summary>
public sealed partial class ConstructedAbilityService
{
    /// <summary>真实交锋的附带作用已提交后通知，直接人物命中沿原事件。</summary>
    public static event Action<ConstructedCorrespondenceEffects> CorrespondenceEffectsConfirmed;

    /// <summary>沿原人物恢复、状态和受力入口执行明确许可，结算后发布实际量。</summary>
    /// <param name="attack">实际来袭面。</param><param name="image">真实映像面。</param>
    /// <param name="target">仍有效的原人物。</param><param name="impact">实际阶段作用。</param>
    /// <param name="effects">双方允许的附带作用。</param><param name="result">原近战结果。</param>
    /// <param name="consumed">实际消耗。</param><param name="point">共同交锋中心。</param>
    /// <param name="priorVelocity">同次原版受击前的运动。</param>
    private static void ApplyCorrespondenceEffects(in ShapeMotion attack, in ShapeMotion image, Actor target,
        CompiledConstructionImpact impact, ConstructionCorrespondenceEffects effects, ApplyAttackState result,
        float consumed, Vector3 point, Vector3? priorVelocity)
    {
        RunningCast running = attack.Running;
        int node = attack.To.Node;
        CastAuthority owner = running.Owner(node);
        long generation = worldGeneration, targetId = target.id;
        float health = target.data.health;
        if ((effects & ConstructionCorrespondenceEffects.Restoration) != 0)
            ApplyRestoration(running, node, target, impact.Restoration, forceBasis: consumed);
        float restored = target.data.health - health;
        float stagger = 0f, bound = 0f;
        if ((effects & ConstructionCorrespondenceEffects.Status) != 0)
        {
            ApplyStatuses(running, node, target, impact.Statuses, forceBasis: consumed);
            ApplyContactReactions(target, impact, owner, result, out stagger, out bound, out _, out _);
        }

        Vector3 velocity = Vector3.zero;
        bool force = false;
        if ((effects & ConstructionCorrespondenceEffects.Force) != 0)
        {
            velocity = ApplyContactForce(running, node, target, impact, result, out force, point,
                priorVelocity: priorVelocity, allowRouting: false);
            velocity += ApplyContactKnockback(running, node, target, impact.Knockback, result, out bool knocked, point, 1f);
            force |= knocked;
        }
        if (generation != worldGeneration) return;
        if (stagger > 0f || bound > 0f) InterruptContactBody(target, Mathf.Max(stagger, bound));
        if (generation != worldGeneration) return;
        var relation = image.Running.Cast.Plan.Nodes[image.To.Node].Correspondence;
        CorrespondenceEffectsConfirmed?.Invoke(new ConstructedCorrespondenceEffects(running.Id, image.Running.Id,
            owner.Id, targetId, relation.Id, simulation.contactTime, effects, consumed, restored, force, velocity));
    }
}
