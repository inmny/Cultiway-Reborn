using System;
using Cultiway.Content.Components;
using Cultiway.Content.Const;
using Cultiway.Core;
using Cultiway.Core.Libraries;
using Cultiway.Core.Progression;
using Cultiway.Utils.Extension;
using UnityEngine;

namespace Cultiway.Content.YaoBeasts;

/// <summary>凝丹结果的有效载荷，供天劫成功结算使用。</summary>
public sealed class YaoCoreCondensationResult
{
    /// <summary>凝丹确定的妖丹方向编号。</summary>
    public string PatternId;

    /// <summary>凝丹时一次确定的品质 0..100。</summary>
    public float Quality;
}

/// <summary>妖丹凝结与天劫维护服务。</summary>
public static class YaoCoreService
{
    /// <summary>按编号读取妖丹方向的本地化名称。</summary>
    public static string GetPatternName(string patternId)
    {
        YaoCorePatternAsset pattern = YaoCorePatterns.Get(patternId);
        return pattern == null ? patternId : pattern.NameKey.Localize();
    }

    /// <summary>开始凝丹准备；准备字段直接保存在妖修组件中。</summary>
    public static bool TryStartPreparation(ActorExtend actor)
    {
        if (actor?.Base == null || actor.Base.isRekt() || !actor.HasCultisys<Yao>()) return false;
        ref Yao yao = ref actor.GetCultisys<Yao>();
        if (!string.IsNullOrEmpty(yao.CorePreparationPatternId)) return true;
        if (yao.BodyStability < YaoSetting.BodyStabilityLowThreshold) return false;
        if (actor.E.HasComponent<YaoTribulation>()) return false;

        YaoCorePatternAsset pattern = YaoCorePatterns.ResolveBest(actor, ResolveBloodlineId(actor));
        if (pattern == null || yao.BodyStability < pattern.RequiredStability) return false;

        yao.CorePreparationStartedAt = YaoTime.Now;
        yao.CorePreparationPatternId = pattern.Id;
        yao.CorePreparationRequiredEssence = 6f;
        yao.CorePreparationRequiredYaoPower = actor.Base.stats[BaseStatses.MaxYaoPower.id] * 0.5f;
        yao.CorePreparationRequiredStability = pattern.RequiredStability;
        actor.GetCultisys<Yao>() = yao;
        return true;
    }

    /// <summary>取消凝丹准备；按原因结算已预留的资源。</summary>
    public static void CancelPreparation(ActorExtend actor, bool penalize)
    {
        if (!actor.HasCultisys<Yao>()) return;
        ref Yao yao = ref actor.GetCultisys<Yao>();
        if (string.IsNullOrEmpty(yao.CorePreparationPatternId)) return;

        yao.CorePreparationStartedAt = -1f;
        yao.CorePreparationPatternId = null;
        if (penalize)
        {
            yao.BodyStability = Mathf.Max(0f, yao.BodyStability - 6f);
            YaoResourceService.Clear(actor, ref yao);
        }

        actor.GetCultisys<Yao>() = yao;
    }

    /// <summary>准备是否已经完成；完成即进入天劫。</summary>
    public static bool IsPreparationComplete(ActorExtend actor, ref Yao yao)
    {
        if (string.IsNullOrEmpty(yao.CorePreparationPatternId)) return false;
        float elapsed = YaoTime.Now - yao.CorePreparationStartedAt;
        if (elapsed < 60f) return false;
        return yao.BodyStability >= yao.CorePreparationRequiredStability;
    }

    /// <summary>凝丹判定：按准备质量与稳定度当场确定妖丹方向与品质。</summary>
    public static ProgressionResolution ResolveCondensation(
        ActorExtend actor, CultisysAsset<Yao> cultisys, ref Yao yao)
    {
        if (string.IsNullOrEmpty(yao.CorePreparationPatternId))
            return ProgressionResolution.Failure(null, "yao.preparation_missing");
        float quality = Mathf.Clamp(
            45f + yao.BodyStability * 0.35f + (yao.Seed % 10) * 0.5f, 10f, 100f);
        return ProgressionResolution.Success(new YaoCoreCondensationResult
        {
            PatternId = yao.CorePreparationPatternId,
            Quality = quality,
        });
    }

    /// <summary>凝丹天劫成功：把妖丹写入核心数据并清空准备字段。</summary>
    public static void CommitCondensation(ActorExtend actor, ref Yao yao, YaoCoreCondensationResult result)
    {
        if (result == null) return;
        YaoCore core = actor.E.HasComponent<YaoCore>()
            ? actor.E.GetComponent<YaoCore>()
            : new YaoCore();

        core.CorePatternId = result.PatternId;
        core.Quality = result.Quality;
        core.Strength = 1f + result.Quality / 40f;
        core.Stability = Mathf.Min(100f, 60f + result.Quality * 0.3f);
        core.Cracks = 0;
        core.CondensationCount++;
        actor.E.AddComponent(core);

        yao.CorePreparationStartedAt = -1f;
        yao.CorePreparationPatternId = null;
        actor.GetCultisys<Yao>() = yao;

        // 妖丹方向提升妖力容量：直接提高稳定度奖励由小境界继续承担。
        YaoResourceService.Gain(actor, ref yao, 10f);
        YaoWorldLog.CoreCondensed(actor, result.PatternId, result.Quality);
    }

    /// <summary>凝丹失败：裂丹、妖力上限受损并进入冷却。</summary>
    public static void ApplyCrackedCore(ActorExtend actor, ref Yao yao, int cracks)
    {
        if (!actor.E.TryGetComponent(out YaoCore core))
        {
            core = new YaoCore
            {
                CorePatternId = yao.CorePreparationPatternId,
                Quality = 10f,
            };
        }

        core.Cracks += cracks;
        core.Stability = Mathf.Max(0f, core.Stability - 25f * cracks);
        actor.E.AddComponent(core);

        yao.CorePreparationStartedAt = -1f;
        yao.CorePreparationPatternId = null;
        actor.GetCultisys<Yao>() = yao;
        YaoWorldLog.CoreCracked(actor, core.Cracks);
    }

    /// <summary>读取妖兽当前主血脉编号；没有血脉时为 null。</summary>
    public static string ResolveBloodlineId(ActorExtend actor)
    {
        return actor.E.TryGetComponent(out YaoGenome genome) ? genome.PrimaryBloodlineId : null;
    }
}

/// <summary>凝丹准备的长期准备阶段：寻找安全地点并积累资源。</summary>
public sealed class YaoCorePreparationStage : IProgressionStage
{
    private const string WorkOrderId = "yao_core_preparation";

    /// <summary>无副作用地读取准备状态。</summary>
    public ProgressionGateResult Evaluate(ProgressionStageContext context)
    {
        if (!context.Actor.E.TryGetComponent(out Yao yao)) return ProgressionGateResult.Blocked("yao.not_yao");
        if (!string.IsNullOrEmpty(yao.CorePreparationPatternId))
        {
            return YaoCoreService.IsPreparationComplete(context.Actor, ref yao)
                ? ProgressionGateResult.Satisfied
                : ProgressionGateResult.InProgress("yao.preparing_core", WorkOrderId);
        }

        // 稳定度过低时暂停新的凝丹准备。
        if (yao.BodyStability < YaoSetting.BodyStabilityLowThreshold)
            return ProgressionGateResult.Blocked("yao.body_stability_low");
        return ProgressionGateResult.NeedsStart("yao.core_ready_to_prepare", WorkOrderId);
    }

    /// <summary>启动凝丹准备：由进阶工作在安全地点调用，这里直接登记准备状态。</summary>
    public void Start(ProgressionStageContext context)
    {
        YaoCoreService.TryStartPreparation(context.Actor);
    }
}
