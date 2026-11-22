using System.Collections.Generic;
using System.Linq;
using Cultiway.Core;
using Cultiway.Core.EventSystem;
using Cultiway.Core.EventSystem.Events;
using Cultiway.Core.SkillLibV3.Visuals;
using Cultiway.Core.Skills;
using Cultiway.Utils;
using Cultiway.Utils.Extension;
using UnityEngine;

namespace Cultiway.Content.Skills;

public sealed partial class ConstructedAbilityService
{
    /// <summary>按原落地范围取对象，实际换位前固定对象，保留其他作用的原顺序。</summary>
    /// <param name="running">真实施放。</param><param name="node">实际落地部位。</param>
    /// <param name="area">本次落地范围与逐发强度。</param><param name="source">本次归属与境界。</param>
    /// <param name="actor">仍有效的原施法者。</param><param name="point">实际落地点。</param>
    private static void ConfirmUtilityLanding(RunningCast running, int node, LegacyAreaImpact area, CastAuthority source,
        Actor actor, Vector3 point)
    {
        long generation = worldGeneration;
        CompiledConstructionImpact impact = running.Cast.Impact(node);
        float force = area.UtilityForce.Value;
        var carrier = running.SpatialSource;
        if (carrier?.data?.id != running.SpatialSourceId || !running.SpatialSourceLive || !carrier.isAlive()) carrier = actor;
        long carrierId = carrier?.data?.id ?? 0;
        var recipients = SkillUtils.IterEnemyInSphere(point, area.Radius, actor, source.AttackKingdom())
            .Select(target => (Target: target, Id: target.getID()));
        if (impact.Exchange != null && carrier != null) recipients = recipients.ToArray();
        foreach (var (target, id) in recipients)
        {
            if (target.isRekt() || target.getID() != id) continue;
            if (target.isActor() && !ApplyUtilityStatuses(target.a, source, impact.Statuses, force, generation)) return;

            foreach (UtilityLandingStep step in area.UtilitySteps)
            {
                if (target.isRekt() || target.getID() != id) break;
                Actor liveCarrier = carrier?.data?.id == carrierId && carrier.isAlive() ? carrier : null;
                switch (step)
                {
                    case UtilityLandingStep.AdditionalDamage:
                        ApplyUtilityDamage(target, actor, source.Damage, impact.AdditionalDamage, force);
                        break;
                    case UtilityLandingStep.Knockback when target is Actor receiver:
                        ApplyContactKnockback(running, node, receiver, impact.Knockback, ApplyAttackState.Hit, out _, point, 1f);
                        break;
                    case UtilityLandingStep.Exchange when target is Actor receiver:
                        ApplyContactExchange(liveCarrier, receiver, impact.Exchange, ApplyAttackState.Hit, 1f, useTiles: true);
                        break;
                    case UtilityLandingStep.Restoration:
                        RestoreUtilityCarrier(liveCarrier, force * impact.Restoration.HealthPerForce);
                        break;
                    case UtilityLandingStep.Random when target is Actor receiver:
                        var choice = impact.RandomContact;
                        if (choice?.HasEffect != true || !Randy.randomChance(choice.Chance)) break;
                        var outcome = choice.Outcomes[Randy.randomInt(0, choice.Outcomes.Count)];
                        if (!ApplyUtilityStatuses(receiver, source, outcome.Statuses, force, generation)) return;
                        ApplyUtilityDamage(receiver, actor, source.Damage, outcome.Damage, force);
                        ApplyContactKnockback(running, node, receiver, outcome.Knockback, ApplyAttackState.Hit, out _, point, 1f);
                        break;
                    case UtilityLandingStep.Explosion:
                        ConfirmUtilityExplosion(target.GetSimPos(), area.Element, source, actor, impact.Spreads, force);
                        break;
                    case UtilityLandingStep.DeathSentence when target is Actor receiver:
                        var sentence = impact.ConditionalContacts.First(rule => rule.MaximumHealthRatio.HasValue);
                        var judgment = receiver.getHealthRatio() <= sentence.MaximumHealthRatio.Value ? sentence.Outcome : sentence.Otherwise;
                        ApplyUtilityDamage(receiver, actor, source.Damage, judgment.Damage, force);
                        break;
                    case UtilityLandingStep.ReincarnationTrial:
                        var trial = impact.ConditionalContacts.First(rule => !rule.MaximumHealthRatio.HasValue);
                        ApplyUtilityTrial(target, actor, liveCarrier, source.Damage, trial, area.Element, force);
                        break;
                }
                if (generation != worldGeneration) return;
            }
        }
    }

    /// <summary>共享状态仍使用本次强度与来源，世界被替换时停止提交。</summary>
    /// <param name="target">实际人物。</param><param name="source">归属及境界。</param>
    /// <param name="statuses">本次状态资料。</param><param name="force">原逐发强度。</param><param name="generation">实际世界。</param>
    private static bool ApplyUtilityStatuses(Actor target, CastAuthority source, IReadOnlyList<CompiledConstructionStatus> statuses,
        float force, long generation)
    {
        foreach (var status in statuses)
        {
            ApplyStatus(target, source, status, 1f, force);
            if (generation != worldGeneration) return false;
        }
        return true;
    }

    /// <summary>功能术沿原人物受击队列，建筑仍使用原版直接受击。</summary>
    /// <param name="target">原范围内的实际对象。</param><param name="actor">施法归属人物，可为空。</param>
    /// <param name="damage">已经保存来源与境界的受击资料。</param><param name="effects">本次伤害资料。</param>
    /// <param name="force">原逐发强度。</param>
    private static void ApplyUtilityDamage(BaseSimObject target, Actor actor, GetHitEvent damage,
        IReadOnlyList<CompiledConstructionContactDamage> effects, float force)
    {
        long generation = worldGeneration;
        foreach (var effect in effects)
        {
            float amount = force * effect.PerForce;
            if (target is Actor receiver)
                amount += receiver.getHealth() * effect.PerHealth + Mathf.Max(effect.MinimumBonus, receiver.getMaxHealth() * effect.PerMaxHealth);
            if (amount <= 0f) continue;
            damage.Element = effect.Element;
            damage.IgnoreDamageReduction = effect.IgnoreDamageReduction;
            QueueUtilityDamage(target, actor, damage, amount);
            if (generation != worldGeneration || target.isRekt()) return;
        }
    }

    /// <summary>只传递实际追加伤害，本次落地的其他词条不随爆炸分送。</summary>
    /// <param name="center">实际对象位置。</param><param name="element">本发地面元素。</param>
    /// <param name="source">实际来源及阵营。</param><param name="actor">施法归属人物，可为空。</param>
    /// <param name="spreads">爆炸半径和伤害资料。</param><param name="force">原逐发强度。</param>
    private static void ConfirmUtilityExplosion(Vector3 center, SkillVfxElementAsset element, CastAuthority source, Actor actor,
        IReadOnlyList<CompiledConstructionContactSpread> spreads, float force)
    {
        long generation = worldGeneration;
        foreach (var spread in spreads)
        {
            ApplyUtilityGroundEffect(center, element, spread.Radius, true, actor);
            if (generation != worldGeneration) return;
            foreach (BaseSimObject target in SkillUtils.IterEnemyInSphere(center, spread.Radius, actor, source.AttackKingdom()))
            {
                ApplyUtilityDamage(target, actor, source.Damage, spread.Outcome.Damage, force);
                if (generation != worldGeneration) return;
            }
        }
    }

    /// <summary>轮回试练保留旧的施加前生命预判，实际伤害仍由原受击队列判定。</summary>
    /// <param name="target">实际对象。</param><param name="actor">施法归属人物，可为空。</param>
    /// <param name="carrier">实际承受反噬或恢复的载体。</param><param name="damage">本发受击资料。</param>
    /// <param name="rule">试练伤害、恢复和反噬。</param><param name="element">反噬使用的地面元素。</param>
    /// <param name="force">原逐发强度。</param>
    private static void ApplyUtilityTrial(BaseSimObject target, Actor actor, Actor carrier, GetHitEvent damage,
        CompiledConstructionConditionalContact rule, SkillVfxElementAsset element, float force)
    {
        long generation = worldGeneration;
        bool likelyKills = target.isActor() && target.getHealth() <= force * rule.Outcome.Damage[0].PerForce;
        long targetId = target.getID(), carrierId = carrier?.data?.id ?? 0;
        ApplyUtilityDamage(target, actor, damage, rule.Outcome.Damage, force);
        if (generation != worldGeneration || carrier?.data?.id != carrierId || !carrier.isAlive()) return;
        if (likelyKills)
        {
            RestoreUtilityCarrier(carrier, force * (rule.OnKill.Restoration?.HealthPerForce ?? 0f));
            return;
        }

        foreach (var back in rule.OnSurvive.Damage)
        {
            float amount = force * back.PerForce;
            if (amount <= 0f) continue;
            ApplyUtilityGroundEffect(carrier.GetSimPos(), element, 0f, false, target);
            if (generation != worldGeneration || carrier.data?.id != carrierId) return;
            damage.Element = back.Element;
            damage.BindAttacker(target, targetId);
            QueueUtilityDamage(carrier, null, damage, amount);
        }
    }

    /// <summary>原元素会修改地面，实际提交后刷新共同地表采样。</summary>
    /// <param name="point">实际作用位置。</param><param name="element">已选地面元素。</param>
    /// <param name="radius">原水平范围。</param><param name="area">是否作用于区域。</param><param name="source">实际来源。</param>
    private static void ApplyUtilityGroundEffect(Vector3 point, SkillVfxElementAsset element, float radius, bool area, BaseSimObject source)
    {
        if (element == null) return;
        long generation = worldGeneration;
        SkillGroundFx.ApplyImpact(point, element, radius, area, source, true);
        if (generation != worldGeneration) return;
        float reach = Mathf.Max(3, Mathf.RoundToInt(radius)) + 1;
        simulation.RefreshTerrainIn(new Rect(point.x - reach, point.y - reach, reach * 2f, reach * 2f));
    }

    /// <summary>原回生及试练收获只写给实际载体。</summary>
    /// <param name="carrier">实际载体。</param><param name="amount">按本发强度得到的恢复量。</param>
    private static void RestoreUtilityCarrier(Actor carrier, float amount)
    {
        int health = Mathf.RoundToInt(amount);
        if (carrier != null && health > 0) RestoreLivingActor(carrier, health);
    }

    /// <summary>实际排入人物受击，或立即提交原版建筑受击。</summary>
    /// <param name="target">实际对象。</param><param name="actor">建筑受击归属。</param>
    /// <param name="damage">人物受击归属及其他资料。</param><param name="amount">本次伤害量。</param>
    private static void QueueUtilityDamage(BaseSimObject target, Actor actor, GetHitEvent damage, float amount)
    {
        if (target.isRekt()) return;
        if (target.isActor())
        {
            damage.TargetID = target.getID();
            damage.Damage = amount;
            EventSystemHub.Publish(damage);
        }
        else target.b.getHit(amount, pAttacker: actor);
    }
}
