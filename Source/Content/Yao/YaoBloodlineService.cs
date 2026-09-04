using System;
using Cultiway.Content.Components;
using Cultiway.Core;
using Cultiway.Utils.Extension;
using UnityEngine;

namespace Cultiway.Content.YaoBeasts;

/// <summary>
///     血脉表达、固血与返祖的唯一入口。
///     隐性等位只代表潜力；表达结果始终写入 <see cref="YaoBody" />。
/// </summary>
public static class YaoBloodlineService
{
    /// <summary>启灵节点：表达显性等位中的兼容器官，并判定一次返祖。</summary>
    public static void ExpressAtAwakening(ActorExtend actor, ref YaoGenome genome)
    {
        YaoAtavismResolver.Resolve(actor, YaoAtavismNode.Awakening, ref genome);
    }

    /// <summary>出生表达：把血脉先天器官写入刚启灵后代的真身。</summary>
    public static void ExpressAtBirth(ActorExtend child, ref Yao yao, ref YaoGenome genome)
    {
        if (!YaoBloodlines.TryGet(genome.PrimaryBloodlineId, out YaoBloodlineAsset bloodline)) return;

        foreach ((string organId, int rank, string slotId) in bloodline.InnateOrgans)
        {
            // 纯度决定血脉器官可以表达到的最低等级；低纯度只表达器官本体。
            int expressedRank = Mathf.Max(1, Mathf.RoundToInt(rank * Mathf.Clamp01(genome.PrimaryPurity)));
            YaoFormPlanService.TryAddOrgan(
                child, YaoFormIds.TrueForm, slotId, organId, expressedRank, YaoOrganOrigin.BloodlineExpressed);
        }
    }

    /// <summary>
    ///     大境界固血：每次大境界最多把一个长期使用的吞噬所得器官写入显性等位。
    /// </summary>
    public static void TrySolidify(ActorExtend actor, ref Yao yao)
    {
        if (!actor.E.TryGetComponent(out YaoGenome genome)) return;
        if (!actor.E.TryGetComponent(out YaoBody body) || !body.TryGetActiveForm(out YaoFormRecord form)) return;
        if (YaoTime.Now < genome.SolidificationSuppressedUntil) return;
        if (actor.E.TryGetComponent(out YaoCore core) && core.Cracks > 0) return;

        // 只挑一个吞噬所得且已稳定使用的器官；没有合格对象时静默跳过。
        foreach (YaoOrganRecord organ in form.Organs)
        {
            if (organ.Origin != YaoOrganOrigin.Digested) continue;
            if (organ.Rank < 2) continue;
            int locusIndex = ResolveLocusIndex(organ.SlotId);
            if (locusIndex < 0) continue;

            genome.EnsureLoci();
            YaoGeneLocus locus = genome.Loci[locusIndex];
            // 旧等位只能被替换或降为隐性，不能无限追加。
            if (!string.IsNullOrEmpty(locus.DominantOrganId) && locus.DominantOrganId != organ.OrganId)
            {
                if (string.IsNullOrEmpty(locus.RecessiveOrganId))
                {
                    locus.RecessiveOrganId = locus.DominantOrganId;
                    locus.RecessiveWeight = locus.DominantWeight * 0.5f;
                }
            }

            locus.DominantOrganId = organ.OrganId;
            locus.DominantWeight = Mathf.Clamp01(0.6f + genome.PrimaryPurity * 0.3f);
            genome.Loci[locusIndex] = locus;
            genome.GenomeGeneration++;
            genome.LastSolidificationReason = "yao.solidify.major_breakthrough";
            actor.E.GetComponent<YaoGenome>() = genome;
            YaoWorldLog.Solidified(actor, organ.OrganId);
            return;
        }
    }

    /// <summary>按槽位返回对应的遗传位点编号；不是八槽位之一的槽位没有位点。</summary>
    public static int ResolveLocusIndex(string slotId)
    {
        // 位点顺序与妖兽八槽基线的固定顺序一致。
        return slotId switch
        {
            YaoContent.Slots.Surface => 0,
            YaoContent.Slots.Head => 1,
            YaoContent.Slots.Breath => 2,
            YaoContent.Slots.Limbs => 3,
            YaoContent.Slots.Metabolism => 4,
            YaoContent.Slots.Spirit => 5,
            YaoContent.Slots.Tail => 6,
            YaoContent.Slots.Heart => 7,
            _ => -1,
        };
    }
}

/// <summary>返祖触发节点。</summary>
public enum YaoAtavismNode : byte
{
    /// <summary>出生表达。</summary>
    Birth,

    /// <summary>凡兽启灵。</summary>
    Awakening,

    /// <summary>妖修大境界成功提交。</summary>
    MajorBreakthrough
}

/// <summary>
///     返祖判定器：只在出生、启灵与大境界提交三个节点各判定一次。
///     成功时替换一个器官、显现一个低阶血脉器官或进入一条固定形态路线。
/// </summary>
public static class YaoAtavismResolver
{
    /// <summary>执行一次返祖判定；失败时保留当前形态。</summary>
    public static void Resolve(ActorExtend actor, YaoAtavismNode node, ref YaoGenome genome)
    {
        if (!YaoBloodlines.TryGet(genome.PrimaryBloodlineId, out YaoBloodlineAsset bloodline)) return;

        float purity = genome.PrimaryPurity;
        // 返祖概率的最高值受纯度约束。
        float chance = Mathf.Clamp01(0.15f + purity * 0.35f);
        if (Randy.randomFloat(0f, 1f) > chance) return;

        string organId = PickAtavismOrgan(actor, bloodline);
        if (organId == null) return;

        string slotId = ResolveOrganSlot(organId);
        if (slotId == null) return;

        // 返祖成功：用隐性等位器官替换当前槽位，保持形态不变。
        if (YaoFormPlanService.TryReplaceOrgan(
                actor, YaoFormIds.TrueForm, slotId, organId, 1, YaoOrganOrigin.Atavistic))
        {
            genome.VisibleAtavismCount++;
            actor.E.GetComponent<YaoGenome>() = genome;
            YaoWorldLog.AtavismCompleted(actor, node, organId);
        }
    }

    private static string PickAtavismOrgan(ActorExtend actor, YaoBloodlineAsset bloodline)
    {
        if (bloodline.AtavismOrganPool == null || bloodline.AtavismOrganPool.Length == 0) return null;
        string[] pool = bloodline.AtavismOrganPool;
        string chosen = pool[Randy.randomInt(0, pool.Length)];

        // 已经长出的器官不再重复返祖。
        if (actor.E.TryGetComponent(out YaoBody body) &&
            body.TryGetActiveForm(out YaoFormRecord form))
        {
            foreach (YaoOrganRecord organ in form.Organs)
            {
                if (string.Equals(organ.OrganId, chosen, StringComparison.Ordinal)) return null;
            }
        }

        return chosen;
    }

    private static string ResolveOrganSlot(string organId)
    {
        CreatureCompositions.Libraries.CreatureOrganAsset asset =
            Content.Libraries.Manager.CreatureOrganLibrary.get(organId);
        if (asset?.SlotRequirements is { Length: > 0 }) return asset.SlotRequirements[0].SlotId;
        return null;
    }
}
