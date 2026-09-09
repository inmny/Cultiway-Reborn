using Cultiway.Content.Components;
using Cultiway.Core;
using Cultiway.Utils.Extension;
using UnityEngine;

namespace Cultiway.Content.YaoBeasts;

/// <summary>
///     双亲遗传的唯一入口。在原版 BabyMaker.makeBaby 完成之后固定孩子的遗传结果；
///     父母之后的变化不会回头改写这个孩子。
/// </summary>
public static class YaoInheritanceService
{
    /// <summary>出生后结算一次固定遗传结果；由 PatchBabyMaker 的后置处理调用。</summary>
    public static void ResolveBirth(Actor child, Actor parent1, Actor parent2)
    {
        if (child == null || parent1 == null || parent2 == null) return;
        if (!parent1.GetExtend().E.TryGetComponent(out YaoGenome genome1)) return;
        if (!parent2.GetExtend().E.TryGetComponent(out YaoGenome genome2)) return;

        // 立即深拷贝双亲当前状态，形成只在本次同步调用中使用的临时资料。
        var payload = new YaoBirthPayload
        {
            Parent1Genome = genome1.DeepCopy(),
            Parent2Genome = genome2.DeepCopy(),
            Parent1BodyPlanId = ResolveBodyPlan(parent1),
            Parent2BodyPlanId = ResolveBodyPlan(parent2),
            BirthSeed = child.data.id.GetHashCode() & 0x7fffffff,
        };

        // 合并主血脉与隐性血脉，并重新归一纯度。
        YaoGenome childGenome = MergeGenomes(payload);
        bool latentIsAwakened = Randy.randomFloat(0f, 1f) <
                                ResolveAwakenedOffspringChance(parent1, parent2, childGenome);

        if (latentIsAwakened)
        {
            // 启灵后代：从启灵 0 级开始，绝不继承父母境界。
            child.GetExtend().NewCultisys(Cultisyses.Yao);
            ref Yao yao = ref child.GetExtend().GetCultisys<Yao>();
            if (!YaoContent.YaoSpeciesTemplates.TryCreateTrueForm(child.asset.id, child.GetExtend()))
            {
                // 物种缺少模板时退化为潜伏凡兽，只保留基因。
                child.GetExtend().E.RemoveComponent<Yao>();
                child.GetExtend().E.AddComponent(childGenome);
                return;
            }

            YaoBloodlineService.ExpressAtBirth(child.GetExtend(), ref yao, ref childGenome);
            YaoAtavismResolver.Resolve(child.GetExtend(), YaoAtavismNode.Birth, ref childGenome);
            child.GetExtend().E.AddComponent(childGenome);
        }
        else
        {
            // 潜伏凡兽：只保存基因，等待以后启灵。
            child.GetExtend().E.AddComponent(childGenome);
        }

        YaoWorldLog.BirthResolved(child.GetExtend(), childGenome.PrimaryBloodlineId, latentIsAwakened);
    }

    /// <summary>启灵后代的概率：随双亲纯度与境界小幅提升，普通妖兽后代多半仍是潜伏凡兽。</summary>
    private static float ResolveAwakenedOffspringChance(Actor parent1, Actor parent2, YaoGenome merged)
    {
        float level = 0f;
        if (parent1.GetExtend().HasCultisys<Yao>()) level += parent1.GetExtend().GetCultisys<Yao>().CurrLevel;
        if (parent2.GetExtend().HasCultisys<Yao>()) level += parent2.GetExtend().GetCultisys<Yao>().CurrLevel;
        return Mathf.Clamp01(0.1f + merged.PrimaryPurity * 0.2f + level * 0.03f);
    }

    private static string ResolveBodyPlan(Actor parent)
    {
        return parent.GetExtend().E.TryGetComponent(out YaoBody body) &&
               body.TryGetActiveForm(out YaoFormRecord form)
            ? form.BodyPlanId
            : null;
    }

    /// <summary>合并双亲基因组：每个位点最多继承两个等位，允许小概率空位变化。</summary>
    private static YaoGenome MergeGenomes(YaoBirthPayload payload)
    {
        var merged = new YaoGenome
        {
            Version = 1,
            PrimaryBloodlineId = payload.Parent1Genome.PrimaryBloodlineId,
            PrimaryPurity = payload.Parent1Genome.PrimaryPurity,
            HiddenBloodlineId = payload.Parent2Genome.PrimaryBloodlineId != payload.Parent1Genome.PrimaryBloodlineId
                ? payload.Parent2Genome.PrimaryBloodlineId
                : payload.Parent2Genome.HiddenBloodlineId,
            HiddenPurity = payload.Parent2Genome.HiddenPurity,
            GenomeGeneration = Mathf.Max(payload.Parent1Genome.GenomeGeneration, payload.Parent2Genome.GenomeGeneration) + 1,
            Seed = payload.BirthSeed,
            ParentId1 = payload.Parent1Genome.ParentId1,
            ParentId2 = payload.Parent2Genome.ParentId1,
        };
        if (string.IsNullOrEmpty(merged.HiddenBloodlineId)) merged.HiddenPurity = 0f;
        merged.EnsureLoci();

        var random = new System.Random(payload.BirthSeed);
        for (int i = 0; i < YaoGenomeSettings.LocusCount; i++)
        {
            YaoGeneLocus locus1 = payload.Parent1Genome.Loci != null && i < payload.Parent1Genome.Loci.Length
                ? payload.Parent1Genome.Loci[i]
                : default;
            YaoGeneLocus locus2 = payload.Parent2Genome.Loci != null && i < payload.Parent2Genome.Loci.Length
                ? payload.Parent2Genome.Loci[i]
                : default;

            // 每个位点从双亲显性、隐性等位中抽取最多两个等位。
            merged.Loci[i] = MergeLocus(locus1, locus2, random);
        }

        return merged;
    }

    private static YaoGeneLocus MergeLocus(YaoGeneLocus a, YaoGeneLocus b, System.Random random)
    {
        string dominant = PickAllele(a.DominantOrganId, b.DominantOrganId, a.DominantWeight, b.DominantWeight, random);
        string recessive = PickAllele(a.RecessiveOrganId, b.RecessiveOrganId, a.RecessiveWeight, b.RecessiveWeight, random);

        // 小概率出现等位降级或空位变化，变化幅度固定。
        if (random.NextDouble() < 0.05) dominant = null;
        if (random.NextDouble() < 0.05 && !string.IsNullOrEmpty(recessive)) recessive = null;

        return new YaoGeneLocus
        {
            DominantOrganId = dominant,
            RecessiveOrganId = recessive == dominant ? null : recessive,
            DominantWeight = Mathf.Clamp01(0.4f + (float)random.NextDouble() * 0.4f),
            RecessiveWeight = Mathf.Clamp01(0.2f + (float)random.NextDouble() * 0.3f),
        };
    }

    private static string PickAllele(string fromA, string fromB, float weightA, float weightB, System.Random random)
    {
        if (string.IsNullOrEmpty(fromA)) return fromB;
        if (string.IsNullOrEmpty(fromB)) return fromA;
        float total = weightA + weightB;
        return total <= 0f || random.NextDouble() < weightA / total ? fromA : fromB;
    }
}

/// <summary>出生计算用的临时资料；只在 ResolveBirth 的同步调用中使用，不进入组件与存档。</summary>
internal struct YaoBirthPayload
{
    /// <summary>父方基因组副本。</summary>
    public YaoGenome Parent1Genome;

    /// <summary>母方基因组副本。</summary>
    public YaoGenome Parent2Genome;

    /// <summary>父方当前身体结构。</summary>
    public string Parent1BodyPlanId;

    /// <summary>母方当前身体结构。</summary>
    public string Parent2BodyPlanId;

    /// <summary>本次出生的稳定种子。</summary>
    public int BirthSeed;
}
