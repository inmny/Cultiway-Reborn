using Cultiway.Content.Combat;
using Cultiway.Core.Combat;
using Cultiway.Core.EventSystem.Systems;
using Cultiway.Core.SkillLibV3.Effects;
using UnityEngine;
using strings;

namespace Cultiway.Content.Skills;

public sealed partial class ConstructedAbilityService
{
    /// <summary>按原修炼关系处理已经由共用立体形状确认的接触。</summary>
    /// <param name="running">本次实际运行的法相。</param><param name="node">确认作用的部位。</param>
    /// <param name="target">实际接触的人物。</param><param name="formation">本次出手固定的作用强度。</param>
    private static ConstructedTargetResponse ApplyFormationAreaContact(RunningCast running, int node, Actor target,
        FormationAreaContact formation)
    {
        CastAuthority owner = running.Owner(node);
        if (!owner.IsLive) return default;
        Actor source = owner.Actor;
        bool enemy = SkillTargetRelationResolver.IsHostile(source, target);
        switch (formation.Kind)
        {
            case FormationAreaKind.Dragon or FormationAreaKind.DragonMight when enemy:
                return ConfirmDragonImpact(running, node, owner, target, formation);
            case FormationAreaKind.Yang:
                if ((target == source || target.kingdom == source.kingdom) &&
                    !SkillTargetRelationResolver.HasHostileRelation(source, target))
                {
                    CombatStatusEffects.CleanseNegativeStatuses(target, 2);
                    CombatResourceEffects.RestoreHealth(target, target.stats[S.health] * .05f * formation.Strength);
                }
                if (enemy)
                    CombatStatusEffects.ApplyTickingStatus(target, StatusEffects.Burn, 4f,
                        source.stats[S.damage] * .05f * formation.Strength, new Core.ElementComposition(pos: 1f), source);
                break;
            case FormationAreaKind.Yin when enemy:
                CombatStatusEffects.ApplyStatus(target, StatusEffects.Freeze, 1f, source);
                CombatStatusEffects.ApplyStatus(target, StatusEffects.Silence, 3f, source);
                DrainFormationWakan(source, target, 12f * formation.Strength);
                break;
        }
        return default;
    }

    /// <summary>从真实震击中心结算伤害、眩晕和沿地震退。</summary>
    /// <param name="running">本次法相。</param><param name="node">实际作用面。</param>
    /// <param name="owner">本次伤害归属。</param><param name="target">身体已经相交的敌人。</param>
    /// <param name="formation">出手时确定的实际强度。</param>
    private static ConstructedTargetResponse ConfirmDragonImpact(RunningCast running, int node, CastAuthority owner,
        Actor target, FormationAreaContact formation)
    {
        long generation = worldGeneration, id = target.id;
        DamageSettlement result = default;
        if (formation.DragonDamage > 0f)
        {
            var hit = owner.Damage;
            hit.TargetID = id;
            hit.Damage = running.Cast.DamageForce(node) * (formation.DragonDamage / hit.Damage);
            result = GetHitEventSystem.Resolve(hit);
        }
        if (worldGeneration != generation || target.data?.id != id || !target.isAlive())
            return new ConstructedTargetResponse(id, Vector3.zero, false, 0f, 0f,
                additionalDamage: formation.DragonDamage > 0f, damage: result);
        bool derived = formation.Kind == FormationAreaKind.DragonMight;
        CombatStatusEffects.ApplyStatus(target, StatusEffects.Daze, derived ? .6f : .8f, owner.Actor);
        Vector2 away = target.current_position - (Vector2)running.Cast.ContactOrigin(node);
        Vector2 force = (away.sqrMagnitude > .0001f ? away.normalized : Vector2.up) *
            ((derived ? 2.5f : 3f) * formation.Strength);
        Vector3 before = ConstructedActorImpulse.Velocity(target);
        bool pushed = ApplyLegacyGroundSlide(target, owner.Actor, force.x, force.y);
        float daze = CombatStatusEffects.GetStatusRemaining(target, StatusEffects.Daze);
        return new ConstructedTargetResponse(id, ConstructedActorImpulse.Velocity(target) - before, pushed,
            daze, 0f, additionalDamage: formation.DragonDamage > 0f, damage: result);
    }
}
