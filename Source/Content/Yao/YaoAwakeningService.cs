using Cultiway.Const;
using Cultiway.Content.Components;
using Cultiway.Content.Const;
using Cultiway.Core;
using Cultiway.Utils.Extension;
using UnityEngine;
using NeoModLoader.api.attributes;

namespace Cultiway.Content.YaoBeasts;

/// <summary>
///     凡兽启灵的唯一入口。启灵积累只由真实事件产生；
///     满足条件后按固定顺序建立妖修体系、身体总表与血脉表达。
/// </summary>
public static class YaoAwakeningService
{
    private static bool hooksRegistered;

    /// <summary>注册启灵积累的全局钩子；只允许模块初始化调用一次。</summary>
    public static void Initialize()
    {
        if (hooksRegistered) return;
        hooksRegistered = true;

        // 新生物：允许启灵的物种才记录启灵积累。
        ActorExtend.RegisterActionOnNewCreature(actor =>
        {
            if (actor.Base == null || actor.HasComponent<YaoAwakeningPotential>()) return;
            bool allowed = false;
            foreach (string speciesId in YaoSetting.AwakeningSpeciesIds)
            {
                if (string.Equals(speciesId, actor.Base.asset.id, System.StringComparison.Ordinal))
                {
                    allowed = true;
                    break;
                }
            }

            if (!allowed) return;

            actor.AddComponent(new YaoAwakeningPotential());
        });

        // 真实捕食：击杀一次加一大截总分。
        ActorExtend.RegisterActionOnKill((self, _, _) =>
        {
            if (!self.TryGetComponent(out YaoAwakeningPotential potential)) return;
            potential.TotalScore += YaoSetting.HuntScoreGain;
            self.E.GetComponent<YaoAwakeningPotential>() = potential;
        });

        // 真实濒死幸存：重伤脱离后积累生存分，并有冷却防止连续刷分。
        ActorExtend.RegisterActionOnDamageResolved((self, _, _, _, _) =>
        {
            if (!self.TryGetComponent(out YaoAwakeningPotential potential)) return;
            if (self.Base.isRekt() || self.Base.getHealthRatio() > 0.2f) return;
            if (YaoTime.Now < potential.NextSurvivalEligibleAt) return;
            potential.TotalScore += YaoSetting.SurvivalScoreGain;
            potential.NextSurvivalEligibleAt = YaoTime.Now + YaoSetting.SurvivalScoreCooldown;
            self.E.GetComponent<YaoAwakeningPotential>() = potential;
        });
    }

    /// <summary>
    ///     按所在地块的灵气浓度积累启灵总分；由低频系统定期调用，不要求地块存在灵脉。
    /// </summary>
    [Hotfixable]
    public static void AccrueExposure(ActorExtend actor, ref YaoAwakeningPotential potential)
    {
        WorldTile tile = actor.Base.current_tile;
        if (tile?.data == null) return;

        float clean = WorldWakanService.GetClean(tile);
        if (clean <= 0f || WorldWakanService.MaximumValue <= 0f) return;

        float ratio = Mathf.Clamp01(clean / WorldWakanService.MaximumValue);
        potential.TotalScore += ratio * YaoSetting.ExposureMaxGainPerEvaluation;
    }

    /// <summary>判断启灵总分是否达标；只读，不修改角色。</summary>
    public static bool MeetsAwakeningThresholds(ActorExtend actor, ref YaoAwakeningPotential potential)
    {
        if (actor?.Base == null || actor.Base.isRekt()) return false;
        if (actor.HasCultisys<Yao>() || actor.HasCultisys<Xian>() ||
            actor.HasCultisys<Knight>() || actor.HasCultisys<Magic>())
            return false;

        return potential.TotalScore >= YaoSetting.AwakeningMinScore;
    }

    /// <summary>满足启灵条件时执行启灵并返回真；按固定顺序建立体系、真身与血脉表达。</summary>
    public static bool TryAwaken(ActorExtend actor)
    {
        if (!MeetsAwakeningThresholds(actor, ref actor.E.GetComponent<YaoAwakeningPotential>())) return false;

        // 按固定顺序执行：接入体系 → 建立真身 → 表达血脉与返祖 → 写世界日志。
        actor.NewCultisys(Cultisyses.Yao);
        ref Yao yao = ref actor.GetCultisys<Yao>();

        if (!YaoContent.YaoSpeciesTemplates.TryCreateTrueForm(actor.Base.asset.id, actor))
        {
            ModClass.LogError($"物种 {actor.Base.asset.id} 缺少启灵身体模板，启灵中止");
            return false;
        }

        // 已经携带潜伏血脉的个体在启灵时表达血脉并尝试返祖。
        if (actor.E.TryGetComponent(out YaoGenome genome) && !string.IsNullOrEmpty(genome.PrimaryBloodlineId))
        {
            YaoBloodlineService.ExpressAtAwakening(actor, ref genome);
        }

        actor.E.RemoveComponent<YaoAwakeningPotential>();

        YaoWorldLog.Awakened(actor, actor.Base.asset.id);
        return true;
    }
}
