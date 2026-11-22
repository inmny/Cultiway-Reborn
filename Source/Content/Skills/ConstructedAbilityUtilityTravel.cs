using System;
using Cultiway.Const;
using Cultiway.Content.Persistence;
using Cultiway.Utils;
using Cultiway.Utils.Extension;
using UnityEngine;

namespace Cultiway.Content.Skills;

/// <summary>一次旧功能术的跟随取样和引力资料，在实际发射时确定。</summary>
internal readonly struct LegacyUtilityTravel(Func<Vector3> trackTarget, LegacyUtilityGravity gravity)
{
    internal readonly Func<Vector3> TrackTarget = trackTarget;
    internal readonly LegacyUtilityGravity Gravity = gravity;
}

/// <summary>原引力的范围、力度和生效阶段。瞬时法术只在成形完成时计算一次。</summary>
internal readonly struct LegacyUtilityGravity(float radius, float strength, float from, float until)
{
    internal readonly float Radius = radius;
    internal readonly float Strength = strength;
    internal readonly float From = from;
    internal readonly float Until = until;
}

public sealed partial class ConstructedAbilityService
{
    /// <summary>只在已经确认的世界步调内施力，接触细分和预测不重复修改人物。</summary>
    private void ConfirmUtilityGravity(RunningCast running, long generation)
    {
        LegacyUtilityGravity rule = running.UtilityTravel.Gravity;
        if (rule.Strength <= 0f || rule.Radius <= 0f || running.Replaced || running.WorldScope?.IsValid() == false) return;
        float age = running.Cast.Age;
        bool instant = rule.From == rule.Until;
        if (age < rule.From || !instant && age > rule.Until) return;
        int tick = instant ? 1 : 1 + Mathf.FloorToInt((age - rule.From) / PerformanceSettings.FixedSimulationStepSeconds);
        if (tick <= running.UtilityGravityTicks) return;
        running.UtilityGravityTicks = tick;

        CastAuthority owner = running.Owner(0);
        Actor source = owner.IsLive ? owner.Actor : null;
        if (!owner.WorldSource && source == null) return;
        Vector3 centre = running.Cast.ContactOrigin(0);
        // 原引力的竖向差以离地坐标计算。独立主体使用出生时的坐标零点，跟随主体使用当前落点地面。
        centre.z -= running.TrackTarget == null
            ? running.UtilityGravityGround
            : terrain?.Sample(centre.x, centre.y) ?? 0f;
        foreach (BaseSimObject candidate in SkillUtils.IterEnemyInSphere(centre, rule.Radius, source, owner.AttackKingdom()))
        {
            if (!candidate.isActor() || candidate.isRekt()) continue;
            Actor actor = candidate.a;
            if (!SkillModifiers.TryApplyGravity(actor, source, centre, rule.Radius, rule.Strength)) continue;
            ActorPositionSaveService.Remember(actor.GetExtend());
            ConstructedActorImpulse.BeginCombat(actor, 0f, terrain);
            if (generation != worldGeneration) return;
            NotifyActorChanged(actor);
        }
    }
}
