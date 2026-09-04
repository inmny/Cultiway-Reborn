using Cultiway.Content.Components;
using Cultiway.Content.CreatureCompositions.Combat;
using Cultiway.Core;
using Cultiway.Utils.Extension;
using UnityEngine;

namespace Cultiway.Content.YaoBeasts;

/// <summary>妖兽器官效果类别的实际执行体；全部经过统一分发器调用。</summary>
public static class YaoOrganEffects
{
    /// <summary>毒系受击反应：被击杀毒牙妖兽的攻击者有概率中毒。</summary>
    public static void OnVenom(ref CreatureOrganEffectContext context)
    {
        if (context.Attacker == null || !context.Attacker.isActor() || context.Attacker.a.isRekt()) return;
        // 等级越高概率越高：一级 25%，二级 45%。
        float chance = context.Rank >= 2 ? 0.45f : 0.25f;
        if (Randy.randomFloat(0f, 1f) > chance) return;
        context.Attacker.a.addStatusEffect("poisoned", 5f + context.Rank * 2f);
    }

    /// <summary>低阶再生：脱离战斗后的低频恢复，按等级换算妖力消耗。</summary>
    public static void OnRegeneration(ref CreatureOrganEffectContext context)
    {
        ActorExtend owner = context.Owner;
        Actor actor = owner.Base;
                if (actor.isRekt()) return;
        bool inCombat = actor.hasTask() && actor.ai.task.in_combat;
        if (inCombat) return;
        if (actor.getHealthRatio() >= 1f) return;

        float recover = actor.getMaxHealth() * (0.02f * context.Rank);
        if (!YaoResourceService.TrySpend(owner, recover * 0.05f * yaoRecoveryCost(owner))) return;
        actor.restoreHealth(Mathf.Max(1, Mathf.RoundToInt(recover)));
    }

    /// <summary>玄甲姿态：常规适应阶段按等级削减本次伤害。</summary>
    public static void OnTurtleStance(ref CreatureOrganEffectContext context)
    {
        context.Damage *= context.Rank >= 2 ? 0.75f : 0.9f;
    }

    /// <summary>吞天胃：击杀后立即从尸体获得一次精华，无需走到尸体旁。</summary>
    public static void OnGluttony(ref CreatureOrganEffectContext context)
    {
        if (context.Victim == null || context.Victim.isRekt()) return;
        YaoDigestionService.TryClaimKillDirectly(context.Owner, context.Victim);
    }

    /// <summary>
    ///     凤凰涅槃保命：必须当场确认致命伤、当场扣除涅槃次数并改写本次伤害；
    ///     涅槃体阶段交由后果队列延后处理。
    /// </summary>
    public static void OnNirvana(ref CreatureOrganEffectContext context)
    {
        if (context.Damage < ownerHealth(context)) return; // 还不致命，不需要动用涅槃。
        if (!context.Owner.TryGetComponent(out Yao yao)) return;
        if (yao.PhoenixRevivalUses <= 0) return;
        if (context.Owner.E.HasComponent<Nirvana>()) return;

        // 当场消费一次机会并拦下这次死亡。
        yao.PhoenixRevivalUses--;
        context.Owner.E.GetComponent<Yao>() = yao;
        context.Damage = 0f;

        CreatureConsequenceQueue.TryEnqueue(new CreatureConsequenceEntry(
            context.Owner.E,
            context.Owner.E.HasComponent<CreatureCompositions.Components.CreaturePhenotype>()
                ? context.Owner.E.GetComponent<CreatureCompositions.Components.CreaturePhenotype>().Revision
                : 0,
            "yao.nirvana",
            context.SlotId,
            context.OrganId,
            -1,
            8f));
    }

    /// <summary>九尾代命：与涅槃同一条保命路径，当场扣除一条尾命。</summary>
    public static void OnNineTailSubstitute(ref CreatureOrganEffectContext context)
    {
        if (context.Damage < ownerHealth(context)) return;
        if (!context.Owner.TryGetComponent(out Yao yao)) return;
        if (yao.NineTailLifeUses <= 0) return;

        yao.NineTailLifeUses--;
        context.Owner.E.GetComponent<Yao>() = yao;
        context.Damage = 0f;
        actorInvince(context.Owner.Base);

        YaoWorldLog.TailLifeSubstituted(context.Owner);
    }

    private static float ownerHealth(CreatureOrganEffectContext context)
    {
        return context.Owner?.Base != null ? context.Owner.Base.getHealth() : 0f;
    }

    private static float yaoRecoveryCost(ActorExtend owner)
    {
        return owner.HasCultisys<Yao>() ? Mathf.Max(0.2f, owner.GetCultisys<Yao>().RecoveryCost) : 1f;
    }

    private static void actorInvince(Actor actor)
    {
        // 代命后的短促无敌，避免同一轮攻击把剩余生命也清空。
        actor.addStatusEffect("shield", 2f);
    }
}
